using Alumni76.Utilities;
using Microsoft.AspNetCore.Http; // For IFormFile
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Alumni76.Utilities
{
    public interface IStorageService
    {
        Task<string> UploadFileAsync(IFormFile file, string containerName, string folderPath = null!);
        Task DeleteFileAsync(string containerName, string filePath);


        Task<List<string>> ListAllBlobNamesAsync();
        Task<int> DeleteBlobsAsync(IEnumerable<string> blobNames);
    }
}