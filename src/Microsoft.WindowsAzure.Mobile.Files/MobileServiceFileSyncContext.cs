using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Eventing;
using Microsoft.WindowsAzure.MobileServices.Files.Eventing;
using Microsoft.WindowsAzure.MobileServices.Files.Metadata;
using Microsoft.WindowsAzure.MobileServices.Files.Operations;
using Microsoft.WindowsAzure.MobileServices.Files.Sync;
using Microsoft.WindowsAzure.MobileServices.Sync;
using Newtonsoft.Json.Linq;

namespace Microsoft.WindowsAzure.MobileServices.Files
{
    public class MobileServiceFileSyncContext : IFileSyncContext, IDisposable
    {
        private readonly IFileOperationQueue operationsQueue;
        private readonly IMobileServiceFilesClient mobileServiceFilesClient;
        private readonly IFileMetadataStore metadataStore;
        private readonly SemaphoreSlim processingSemaphore = new SemaphoreSlim(1);
        private readonly IFileSyncHandler syncHandler;
        private readonly IMobileServiceEventManager eventManager;
        private bool disposed = false;
        private IDisposable changeNotificationSubscription;

        public MobileServiceFileSyncContext(IMobileServiceClient client, IFileMetadataStore metadataStore, IFileOperationQueue operationsQueue, IFileSyncHandler syncHandler)
        {
            if (client == null)
            {
                throw new ArgumentNullException("client");
            }

            if (metadataStore == null)
            {
                throw new ArgumentNullException("metadataStore");
            }

            if (operationsQueue == null)
            {
                throw new ArgumentNullException("operationsQueue");
            }

            if (syncHandler == null)
            {
                throw new ArgumentNullException("syncHandler");
            }

            this.metadataStore = metadataStore;
            this.syncHandler = syncHandler;
            this.operationsQueue = operationsQueue;
            this.mobileServiceFilesClient = new MobileServiceFilesClient(client, new AzureBlobStorageProvider(client));

            this.eventManager = client.EventManager;
            this.changeNotificationSubscription = this.eventManager.Subscribe<StoreOperationCompletedEvent>(OnStoreOperationCompleted);
        }

        private void OnStoreOperationCompleted(StoreOperationCompletedEvent storeOperationEvent)
        {
            switch (storeOperationEvent.Operation.Kind)
            {
                case LocalStoreOperationKind.Insert:
                case LocalStoreOperationKind.Update:
                case LocalStoreOperationKind.Upsert:
                    if (storeOperationEvent.Operation.Source == StoreOperationSource.ServerPull 
                        || storeOperationEvent.Operation.Source == StoreOperationSource.ServerPush)
                    {
                        PullFilesAsync(storeOperationEvent.Operation.TableName, storeOperationEvent.Operation.RecordId);
                    }
                    break;
                case LocalStoreOperationKind.Delete:
                    this.metadataStore.PurgeAsync(storeOperationEvent.Operation.TableName, storeOperationEvent.Operation.RecordId);
                    break;
                default:
                    break;
            }
        }

        internal async Task NotifyFileOperationCompletion(MobileServiceFile file, FileOperationKind fileOperationKind, FileOperationSource source)
        {
            var operationCompletedEvent = new FileOperationCompletedEvent(file, fileOperationKind, source);

            await this.eventManager.PublishAsync(operationCompletedEvent);
        }

        public async Task AddFileAsync(MobileServiceFile file)
        {
            var metadata = new MobileServiceFileMetadata
            {
                FileId = file.Id,
                FileName = file.Name,
                Length = file.Length,
                Location = FileLocation.Local,
                ContentMD5 = file.ContentMD5,
                ParentDataItemType = file.TableName,
                ParentDataItemId = file.ParentId
            };

            await metadataStore.CreateOrUpdateAsync(metadata);

            var operation = new CreateMobileServiceFileOperation(file.Id);

            await QueueOperationAsync(operation);

            NotifyFileOperationCompletion(file, FileOperationKind.Create, FileOperationSource.Local);
        }

        public async Task DeleteFileAsync(MobileServiceFile file)
        {
            var operation = new DeleteMobileServiceFileOperation(file.Id);

            await QueueOperationAsync(operation);

            NotifyFileOperationCompletion(file, FileOperationKind.Delete, FileOperationSource.Local);
        }

        public async Task PushChangesAsync(CancellationToken cancellationToken)
        {
            await processingSemaphore.WaitAsync(cancellationToken);
            try
            {
                while (this.operationsQueue.Count > 0)
                {
                    IMobileServiceFileOperation operation = await operationsQueue.PeekAsync();

                    // This would also take the cancellation token
                    await operation.Execute(this.metadataStore, this);

                    await operationsQueue.RemoveAsync(operation.FileId);
                }
            }
            finally
            {
                processingSemaphore.Release();
            }
        }

        public async Task PullFilesAsync(string tableName, string itemId)
        {
            IEnumerable<MobileServiceFile> files = await this.mobileServiceFilesClient.GetFilesAsync(tableName, itemId);

            foreach (var file in files)
            {
                FileSynchronizationAction syncAction = FileSynchronizationAction.Update;

                MobileServiceFileMetadata metadata = await this.metadataStore.GetFileMetadataAsync(file.Id);

                if (metadata == null)
                {
                    syncAction = FileSynchronizationAction.Update;

                    metadata = MobileServiceFileMetadata.FromFile(file);

                    metadata.ContentMD5 = null;
                    metadata.LastModified = null;
                }

                if (string.Compare(metadata.ContentMD5, file.ContentMD5, StringComparison.Ordinal) != 0 ||
                    (metadata.LastModified == null || metadata.LastModified.Value.ToUniversalTime() != file.LastModified.Value.ToUniversalTime()))
                {
                    metadata.LastModified = file.LastModified;
                    metadata.ContentMD5 = file.ContentMD5;

                    await this.metadataStore.CreateOrUpdateAsync(metadata);
                    await this.syncHandler.ProcessFileSynchronizationAction(file, syncAction);

                    NotifyFileOperationCompletion(file, syncAction.ToFileOperationKind(), FileOperationSource.ServerPull);
                }
            }

            // This is an example of how this would be handled. VERY simple logic right now... 
            var fileMetadata = await this.metadataStore.GetMetadataAsync(tableName, itemId);
            var deletedItemsMetadata = fileMetadata.Where(m => !files.Any(f => string.Compare(f.Id, m.FileId) == 0));

            foreach (var metadata in deletedItemsMetadata)
            {
                //var pendingOperation = this.operations.FirstOrDefault(o=>string.Compare(o.FileId, metadata.FileId) == 0);
                IMobileServiceFileOperation pendingOperation = await this.operationsQueue.GetOperationByFileIdAsync(metadata.FileId);

                // TODO: Need to call into the sync handler for conflict resolution here...
                if (pendingOperation == null || pendingOperation is DeleteMobileServiceFileOperation)
                {
                    await metadataStore.DeleteAsync(metadata);

                    await this.syncHandler.ProcessFileSynchronizationAction(MobileServiceFile.FromMetadata(metadata), FileSynchronizationAction.Delete);

                    NotifyFileOperationCompletion(MobileServiceFile.FromMetadata(metadata), FileOperationKind.Delete, FileOperationSource.ServerPull);
                }
            }
        }

        public async Task<bool> QueueOperationAsync(IMobileServiceFileOperation operation)
        {
            bool operationEnqueued = false;

            await processingSemaphore.WaitAsync();

            try
            {
                var pendingItemOperations = await this.operationsQueue.GetOperationByFileIdAsync(operation.FileId);

                if (pendingItemOperations != null)
                {
                    pendingItemOperations.OnQueueingNewOperation(operation);

                    if (pendingItemOperations.State == FileOperationState.Cancelled)
                    {
                        await this.operationsQueue.DequeueAsync();
                    }
                }

                if (operation.State != FileOperationState.Cancelled)
                {
                    await this.operationsQueue.EnqueueAsync(operation);
                    operationEnqueued = true;
                }

            }
            finally
            {
                processingSemaphore.Release();
            }

            return operationEnqueued;
        }

        public IMobileServiceFilesClient MobileServiceFilesClient
        {
            get { return this.mobileServiceFilesClient; }
        }

        public IFileSyncHandler SyncHandler
        {
            get { return this.syncHandler; }
        }

        public IFileMetadataStore MetadataStore
        {
            get { return this.metadataStore; }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    this.changeNotificationSubscription.Dispose();
                }

                disposed = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }
    }

    internal static class FileSynchronizationActionException
    {
        public static FileOperationKind ToFileOperationKind(this FileSynchronizationAction synchronizationAction)
        {
            switch (synchronizationAction)
            {
                case FileSynchronizationAction.Create:
                    return FileOperationKind.Create;
                case FileSynchronizationAction.Update:
                    return FileOperationKind.Update;
                case FileSynchronizationAction.Delete:
                    return FileOperationKind.Delete;
                default:
                    throw new InvalidOperationException("Unknown FileSynchronizationAction value.");
            }
        }
    }

}
