using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices.Files.Metadata;
using Microsoft.WindowsAzure.MobileServices;
using Microsoft.WindowsAzure.MobileServices.Sync;

namespace Microsoft.WindowsAzure.MobileServices.Files.Operations
{
    public sealed class DeleteMobileServiceFileOperation : MobileServiceFileOperation
    {
        public DeleteMobileServiceFileOperation(string fileId)
            : base(fileId)
        {
        }

        public override FileOperationKind Kind
        {
            get
            {
                return FileOperationKind.Delete;
            }
        }

        protected async override Task ExecuteOperation(IFileMetadataStore metadataStore, IFileSyncContext context)
        {
            MobileServiceFileMetadata metadata = await metadataStore.GetFileMetadataAsync(FileId);

            if (metadata != null)
            {
                await metadataStore.DeleteAsync(metadata);

                await context.MobileServiceFilesClient.DeleteFileAsync(metadata);
            }
        }

        public override void OnQueueingNewOperation(IMobileServiceFileOperation operation)
        {
            //
        }
    }
}
