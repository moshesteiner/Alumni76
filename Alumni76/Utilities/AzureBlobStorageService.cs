using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http; // Required for IFormFile
using Microsoft.Extensions.Options;
using System.IO;

// 💡 The interface is correctly found because it's in the same namespace (Alumni76.Utilities)
// or because you added 'using Alumni76.Utilities;'

namespace Alumni76.Utilities
{
    public class AzureBlobStorageService : IStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly string _containerName;

        //public AzureBlobStorageService(BlobServiceClient blobServiceClient, BlobContainerClient containerClient)
        //{
        //    _blobServiceClient = blobServiceClient;
        //    _containerClient = containerClient ?? throw new ArgumentNullException(nameof(containerClient));
        //}
        public AzureBlobStorageService(BlobServiceClient blobServiceClient, IOptions<StorageOptions> storageOptions)
        {
            _blobServiceClient = blobServiceClient;
            // Get the dynamically selected container name
            _containerName = storageOptions.Value.ContainerName
                ?? throw new InvalidOperationException("StorageOptions: ContainerName is missing.");
        }

        // --- METHOD 1: UploadFileAsync (Corrected to match IStorageService) ---
        public async Task<string> UploadFileAsync(IFormFile file, string containerName, string folderPath = null!)
        {
            BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            // Ensure the container exists and is publicly accessible (if needed)
            //await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: CancellationToken.None);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: CancellationToken.None);

            // 2. Determine the full blob path (GCS "folderPath" + file name)
            string blobPath = string.IsNullOrEmpty(folderPath) ? file.FileName :
                                 $"{folderPath.Trim('/')}/{file.FileName}";

            BlobClient blobClient = containerClient.GetBlobClient(blobPath);

            var blobHttpHeaders = new BlobHttpHeaders
            {
                ContentType = file.ContentType // Use the MIME type provided by the IFormFile
            };
           
            // 3. Upload the file using its stream
            using (var fileStream = file.OpenReadStream())
            {
                await blobClient.UploadAsync( fileStream, overwrite: true);
            }
            await blobClient.SetHttpHeadersAsync(blobHttpHeaders);

            // 4. Return the public URL
            return blobClient.Uri.AbsoluteUri;
        }

        // --- METHOD 2: DeleteFileAsync (Corrected to match IStorageService) ---
        public async Task DeleteFileAsync(string blobPathToDelete, string containerName)
        {
            BlobContainerClient containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            BlobClient blobClient = containerClient.GetBlobClient(blobPathToDelete);
            await blobClient.DeleteIfExistsAsync();             
        }
        public async Task<List<string>> ListAllBlobNamesAsync()
        {
            BlobContainerClient containerClient = GetActiveContainerClient();
            var blobs = new List<string>();
            await foreach (var blobItem in containerClient.GetBlobsAsync(BlobTraits.None, BlobStates.None, ""))
            {
                if (blobItem.Name != null)
                {
                    blobs.Add(blobItem.Name);
                }
            }
            return blobs;
        }

        public async Task<int> DeleteBlobsAsync(IEnumerable<string> blobNames)
        {
            BlobContainerClient containerClient = GetActiveContainerClient();
            var deleteTasks = new List<Task<bool>>();

            foreach (var blobName in blobNames)
            {
                BlobClient blobClient = containerClient.GetBlobClient(blobName);
                deleteTasks.Add(blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots).ContinueWith(t =>
                    t.Result.Value
                ));
            }

            var results = await Task.WhenAll(deleteTasks);

            // Count how many were successfully deleted (i.e., existed)
            return results.Count(wasDeleted => wasDeleted);
        }
        private BlobContainerClient GetActiveContainerClient()
        {
            return _blobServiceClient.GetBlobContainerClient(_containerName);
        }
    }
}