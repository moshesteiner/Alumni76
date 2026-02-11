// File: StorageCleanup.cshtml.cs

using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Bagrut_Eval.Pages.Common;
using Bagrut_Eval.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration; // Required for IConfiguration
using System.Text.Json;

namespace Bagrut_Eval.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class StorageCleanupModel : BasePageModel<StorageCleanupModel>
    {
        private readonly IStorageService _azureService;
        private readonly IConfiguration _configuration;
        private readonly string _containerName;

        // The list of all files in the container, which are the cleanup candidates
        public List<string> OrphanedBlobNames { get; set; } = new List<string>();

        // Properties for display
        public int TotalBlobCount { get; set; }
        public int TotalOrphanedCount { get; set; }
        public string ContainerName { get; set; } = string.Empty;


        // Simplified constructor
        public StorageCleanupModel(ApplicationDbContext dbContext, IConfiguration configuration, IStorageService azureService,
                                 ILogger<StorageCleanupModel> logger, ITimeProvider timeProvider) : base(dbContext, logger, timeProvider)
        {
            _azureService = azureService;
            _configuration = configuration;

            // Assuming container name logic is still needed for display and for the service to know which container to use
            var activeConnectionName = configuration.GetValue<string>("AppSettings:ActiveConnectionName")!;
            string containerKey = activeConnectionName == "AzureDbConnection" ? "AzureContainerName" : "AzureContainerName_Dev";
            _containerName = configuration.GetValue<string>($"StorageSettings:{containerKey}")
                ?? throw new InvalidOperationException($"Configuration Error: StorageSettings:{containerKey} is missing or empty.");

            ContainerName = _containerName;
        }


        public new async Task OnGetAsync()
        {
            await base.OnGetAsync();
            CheckForSpecialAdmin();
            if (!IsSpecialAdmin)
            {
                RedirectToPage("/Index");
                return;
            }
        }
        public async Task<IActionResult> OnPostLoadDataAsync()
        {
            CheckForSpecialAdmin();
            if (!IsSpecialAdmin)
            {
                return RedirectToPage("/Index");
            }

            // Get all blob names from the Azure Storage container
            var allBlobNames = await _azureService.ListAllBlobNamesAsync();

            // Get all references
            var referencedFilePaths = await _dbContext.Drawings
                .Select(d => d.FilePathOrUrl)
                .Where(path => path != null)
                .ToListAsync();

            var referencedFileNames = referencedFilePaths
                .Select(rawPath => NormalizeFilePath(rawPath!, _containerName))
                .Where(normalizedPath => normalizedPath != null)
                .ToList()!;

            OrphanedBlobNames = allBlobNames.Except(referencedFileNames).ToList()!;

            TotalBlobCount = allBlobNames.Count;
            TotalOrphanedCount = OrphanedBlobNames.Count;
            if (TotalOrphanedCount == 0)
            {
                TempData["SuccessMessage"] = $"לא נמצאו קבצים אבודים ב - {ContainerName}.   מספר הקבצים: {TotalBlobCount}";
            }

            return Page(); // redisplay with data
        }


        // The single POST handler to delete all files in the container
        public async Task<IActionResult> OnPostDeleteAllAsync()
        {
            CheckForSpecialAdmin();
            if (!IsSpecialAdmin)
            {
                return RedirectToPage("/Index");
            }

            // Refetch the list to ensure the latest state is deleted (best practice for POST operations)
            var allBlobNames = await _azureService.ListAllBlobNamesAsync();
            // Get all refeneces
            var referencedFilePaths = await _dbContext.Drawings
                        .Select(d => d.FilePathOrUrl)
                        .Where(path => path != null)
                        .ToListAsync();

            //  Strips Azure prefixes
            var referencedFileNames = referencedFilePaths
                            .Select(rawPath => NormalizeFilePath(rawPath!, _containerName))
                            .Where(normalizedPath => normalizedPath != null)
                            .ToList()!;

            OrphanedBlobNames = allBlobNames.Except(referencedFileNames).ToList()!;

            if (!OrphanedBlobNames.Any())
            {
                TempData["ErrorMessage"] = $"The are no files to delete";
                return RedirectToPage();
            }

            // Execute the deletion
            var deletedCount = await _azureService.DeleteBlobsAsync(OrphanedBlobNames);

            // Log the success message to TempData for the RedirectToPage to display
            TempData["SuccessMessage"] = $"Successfully deleted {deletedCount} files from container '{ContainerName}'";

            return RedirectToPage();
        }

        // Remove all other OnPost methods (OnPostDeleteAsync, OnPostVerifyIntersectionAsync, OnPostFinalDeleteAsync)

        private string? NormalizeFilePath(string fullPath, string containerName)
        {
            if (string.IsNullOrEmpty(fullPath))
            {
                return null;
            }

            // We search for the container name prefix: /bagrutevalstorage/
            string fullAzurePrefix = $"/{containerName}/";
            int index = fullPath.IndexOf(fullAzurePrefix, StringComparison.OrdinalIgnoreCase);

            if (index > 0)
            {
                // Strip everything up to and including the container name and the trailing slash.
                return fullPath.Substring(index + fullAzurePrefix.Length);
            }

            //  Handle relative paths (e.g., 'issues-drawings/...')
            if (!fullPath.StartsWith("http", StringComparison.OrdinalIgnoreCase) && fullPath.Contains('/'))
            {
                return fullPath;
            }

            //  Fallback for non-standard path
            return null;
        }
    }
}