// ExportPageModel.cs
using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Security.Claims;
using System;
using System.IO;
using Bagrut_Eval.Pages.Metrics;

namespace Bagrut_Eval.Pages.Metrics
{
    [Authorize(Roles = "Senior, Admin")]
    public class ExportPageModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<ExportPageModel> _logger;

        public ExportPageModel(ApplicationDbContext dbContext, ILogger<ExportPageModel> logger)
        {
            _dbContext = dbContext;
            _logger = logger;
        }

        // <--- Properties for the PageModel --->
        [BindProperty(SupportsGet = true)]
        public int? SelectedExamId { get; set; }
        public string? SelectedExamTitle { get; set; }
        public SelectList? AvailableExams { get; set; }
        public List<Export> Exports { get; set; } = new List<Export>();

        [BindProperty]
        public UpdateInputModel? UpdateInput { get; set; }

        public bool IsShowOnlyExported { get; set; }

        public class UpdateInputModel
        {
            public Dictionary<int, string?> Descriptions { get; set; } = new Dictionary<int, string?>();
            public Dictionary<int, string?> Scores { get; set; } = new Dictionary<int, string?>();
            public Dictionary<int, bool> IsExported { get; set; } = new Dictionary<int, bool>();
            public string? FileName { get; set; } // Add this property
        }


        // <--- Methods for the PageModel --->
        public async Task OnGetAsync()
        {
            ModelState.Clear();
            await LoadDataAsync();
        }

        public async Task<IActionResult> OnPostSelectExamAsync()
        {
            ModelState.Clear();
            if (SelectedExamId.HasValue && SelectedExamId.Value > 0)
            {
                await SyncExportsTableAsync();
                await LoadDataAsync();
            }
            else
            {
                TempData["ErrorMessage"] = "Please select an exam to continue.";
                await LoadDataAsync();
            }

            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAsync()
        {
            await ProcessFormUpdatesAsync(UpdateInput!);
            await LoadDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAndShowAsync()
        {
            await ProcessFormUpdatesAsync(UpdateInput!);
            await LoadDataAsync(showOnlyExported: true);
            return Page();
        }

        // NEW HANDLER: This handler is for the new "Cancel" button on the preview page.
        public async Task<IActionResult> OnPostCancelShowAsync()
        {
            await LoadDataAsync(showOnlyExported: false);
            return Page();
        }

        public async Task<IActionResult> OnPostUpdateAndExportAsync()
        {
            // First, update the database with any changes from the form
            await ProcessFormUpdatesAsync(UpdateInput!);

            // Then, fetch only the items marked for export
            var exportsToDownload = await _dbContext.Exports
                .Include(e => e.Issue)
                    .ThenInclude(i => i!.FinalAnswer)
                .Include(e => e.Issue)
                    .ThenInclude(i => i!.Part)
                .Where(e => e.Issue!.ExamId == SelectedExamId && e.Exported)
                .OrderBy(e => e.Issue!.QuestionNumber)
                .ThenBy(e => e.Issue!.Part!.QuestionPart)
                .ToListAsync();

            if (!exportsToDownload.Any())
            {
                TempData["ErrorMessage"] = "לא נבחרו פריטים לייצוא. נא לסמן את הפריטים הרצויים.";
                await LoadDataAsync();
                return Page();
            }

            // Get the exam title for the document content
            var examTitle = await _dbContext.Exams
                .Where(e => e.Id == SelectedExamId)
                .Select(e => e.ExamTitle)
                .FirstOrDefaultAsync();

            // Sanitize the filename provided by the user
            var sanitizedFileName = SanitizeFileName(UpdateInput!.FileName!);
            if (string.IsNullOrEmpty(sanitizedFileName))
            {
                sanitizedFileName = $"מחוון-{string.Join("_", examTitle!.Split(Path.GetInvalidFileNameChars()))}.docx";
            }
            else if (!sanitizedFileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            {
                sanitizedFileName += ".docx";
            }


            // Generate the Word document
            var memoryStream = DownloadMetrics.GenerateWordDocument(examTitle!, exportsToDownload);

            // Return the file for download with the sanitized filename
            return File(memoryStream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", sanitizedFileName);
        }

        private async Task LoadDataAsync(bool showOnlyExported = false)
        {
            this.IsShowOnlyExported = showOnlyExported;

            var exams = await _dbContext.Exams
                                             .Where(e => e.Active)
                                             .OrderBy(e => e.ExamTitle)
                                             .ToListAsync();

            AvailableExams = new SelectList(exams, "Id", "ExamTitle");

            if (SelectedExamId.HasValue && SelectedExamId.Value > 0)
            {
                SelectedExamTitle = exams.FirstOrDefault(e => e.Id == SelectedExamId.Value)?.ExamTitle;

                var query = _dbContext.Exports
                    .Include(e => e.Issue)
                        .ThenInclude(i => i!.Exam)
                    .Include(e => e.Issue)
                        .ThenInclude(i => i!.Part)
                    .Include(e => e.Senior)
                    .Include(e => e.Issue!.FinalAnswer)
                    .Where(e => e.Issue!.ExamId == SelectedExamId);

                if (showOnlyExported)
                {
                    query = query.Where(e => e.Exported == true);
                }

                Exports = await query
                    .OrderBy(e => e.Issue!.QuestionNumber)
                    .ThenBy(e => e.Issue!.Part!.QuestionPart)
                    .ToListAsync();
            }
            else
            {
                SelectedExamTitle = null;
                Exports = new List<Export>();
            }

            // Populate the input model and set the default filename
            UpdateInput = new UpdateInputModel();
            if (Exports != null)
            {
                foreach (var export in Exports)
                {
                    UpdateInput.IsExported[export.IssueId] = export.Exported;
                    UpdateInput.Descriptions[export.IssueId] = export.Description;
                    UpdateInput.Scores[export.IssueId] = export.Score;
                }
            }
            // Set the default filename here
            if (!string.IsNullOrEmpty(SelectedExamTitle))
            {
                UpdateInput.FileName = $"מחוון-{string.Join("_", SelectedExamTitle.Split(Path.GetInvalidFileNameChars()))}.docx";
            }
        }

        private async Task SyncExportsTableAsync()
        {
            var seniorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var missingIssues = await _dbContext.Issues
                .Where(i => i.ExamId == SelectedExamId && i.Status == IssueStatus.Closed)
                .Where(i => !_dbContext.Exports.Any(e => e.IssueId == i.Id))
                .ToListAsync();

            var newExports = missingIssues.Select(i => new Export
            {
                IssueId = i.Id,
                Description = null,
                Score = null,
                SeniorId = seniorId,
                Date = DateTime.UtcNow,
                Exported = false
            });

            _dbContext.Exports.AddRange(newExports);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Synced Exports table for Exam ID: {ExamId}. Added {Count} new entries.", SelectedExamId, newExports.Count());
        }

        private async Task ProcessFormUpdatesAsync(UpdateInputModel input)
        {
            var seniorId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

            var exportsToUpdate = await _dbContext.Exports
                .Where(e => e.Issue!.ExamId == SelectedExamId)
                .ToListAsync();

            foreach (var export in exportsToUpdate)
            {
                if (input.Descriptions.TryGetValue(export.IssueId, out var newDescription))
                {
                    export.Description = newDescription;
                }

                if (input.Scores.TryGetValue(export.IssueId, out var newScore))
                {
                    export.Score = newScore;
                }

                if (input.IsExported.TryGetValue(export.IssueId, out var newExportedStatus))
                {
                    export.Exported = newExportedStatus;
                }

                export.Date = DateTime.UtcNow;
                export.SeniorId = seniorId;
            }

            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = "הנתונים עודכנו בהצלחה.";
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
            var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
            return sanitized;
        }
    }
}