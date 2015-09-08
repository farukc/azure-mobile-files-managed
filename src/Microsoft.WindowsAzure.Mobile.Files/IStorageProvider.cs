using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices.Files;
using Microsoft.WindowsAzure.MobileServices.Files.Metadata;
using Microsoft.WindowsAzure.MobileServices.Files.Sync;

namespace Microsoft.WindowsAzure.MobileServices.Files
{
    public interface IStorageProvider
    {
        Task DownloadFileToStreamAsync(MobileServiceFile file, Stream stream, StorageToken storageToken);

        Task UploadFileAsync(MobileServiceFileMetadata metadata, IMobileServiceFileDataSource dataSource, StorageToken storageToken);

        Task<Uri> GetFileUriAsync(StorageToken storageToken, string fileName);
    }
}
