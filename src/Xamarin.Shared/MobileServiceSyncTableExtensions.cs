using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MobileServices.Files.Metadata;
using Microsoft.WindowsAzure.MobileServices.Files.Sync;
using Microsoft.WindowsAzure.MobileServices.Sync;

namespace Microsoft.WindowsAzure.MobileServices.Files
{
    public static class MobileServiceSyncTableExtensions
    {
        public async static Task UploadFileAsync<T>(this IMobileServiceSyncTable<T> table, MobileServiceFile file, string filePath)
        {
            IMobileServiceFileDataSource dataSource = new PathMobileServiceFileDataSource(filePath);

            MobileServiceFileMetadata metadata = MobileServiceFileMetadata.FromFile(file);

            IFileSyncContext context = table.MobileServiceClient.GetFileSyncContext();

            await context.MobileServiceFilesClient.UploadFileAsync(metadata, dataSource);
        }

        public async static Task DownloadFileAsync<T>(this IMobileServiceSyncTable<T> table, MobileServiceFile file, string targetPath)
        {
            using (Stream stream = File.Create(targetPath))
            {
                IFileSyncContext context = table.MobileServiceClient.GetFileSyncContext();

                await context.MobileServiceFilesClient.DownloadToStreamAsync(file, stream);
            }
        }
    }
}
