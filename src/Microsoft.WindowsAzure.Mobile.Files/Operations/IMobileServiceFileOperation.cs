using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices.Files.Metadata;

namespace Microsoft.WindowsAzure.MobileServices.Files
{
    public interface IMobileServiceFileOperation
    {
        string FileId { get; }

        FileOperationKind Kind { get; }

        FileOperationState State { get; }

        Task Execute(IFileMetadataStore metadataStore, IFileSyncContext context);

        void OnQueueingNewOperation(IMobileServiceFileOperation operation);
    }
}
