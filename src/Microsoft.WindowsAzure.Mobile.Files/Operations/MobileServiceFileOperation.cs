using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices.Files.Metadata;
using Microsoft.WindowsAzure.MobileServices;

namespace Microsoft.WindowsAzure.MobileServices.Files
{
    public abstract class MobileServiceFileOperation : IMobileServiceFileOperation
    {
        private string fileId;

        public MobileServiceFileOperation(string fileId)
        {
            this.fileId = fileId;
        }

        public string FileId
        {
            get { return this.fileId; }
        }

        public FileOperationState State { get; protected set; }

        public abstract FileOperationKind Kind { get; }

        public async Task Execute(IFileMetadataStore metadataStore, IFileSyncContext context)
        {
            try
            {
                this.State = FileOperationState.InProcess;

                await ExecuteOperation(metadataStore, context);
            }
            catch
            {
                this.State = FileOperationState.Failed;
                throw;
            }

            this.State = FileOperationState.Succeeded;
        }

        protected abstract Task ExecuteOperation(IFileMetadataStore metadataStore, IFileSyncContext context);


        public abstract void OnQueueingNewOperation(IMobileServiceFileOperation operation);
    }
}
