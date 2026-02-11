using Bagrut_Eval.Utilities;
using Microsoft.AspNetCore.Http; // For IFormFile
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Bagrut_Eval.Utilities
{
    public interface IStorageService
    {
        Task<string> UploadFileAsync(IFormFile file, string containerName, string folderPath = null!);
        Task DeleteFileAsync(string containerName, string filePath);


        Task<List<string>> ListAllBlobNamesAsync();
        Task<int> DeleteBlobsAsync(IEnumerable<string> blobNames);
    }
}