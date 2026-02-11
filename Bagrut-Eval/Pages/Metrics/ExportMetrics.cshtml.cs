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
using System;
using System.IO;
using System.Security.Claims;
using Bagrut_Eval.Pages.Metrics;

namespace Bagrut_Eval.Pages.Metrics
{
    //[Authorize(Roles = "Nobody")]
    //[Authorize(Roles = "Senior, Admin")]
    public class ExportMetricsModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;

        // <--- Properties for the PageModel --->
        [BindProperty(SupportsGet = true)]
        public int? SelectedExamId { get; set; }
        public string? SelectedExamTitle { get; set; }
        public SelectList? AvailableExams { get; set; }
        public List<Metric> Metrics { get; set; } = new List<Metric>();

        [BindProperty]
        public string? FileName { get; set; }

        public ExportMetricsModel(ApplicationDbContext dbContext)
        {
            _dbContext = dbContext;
        }
        public async Task OnGetAsync()
        {
            await LoadDataAsync();
        }

        public async Task<IActionResult> OnPostSelectExamAsync()
        {
            await LoadDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostExportAsync()
        {
            // Fetch all metrics for the selected exam
            var metricsToDownload = await _dbContext.Metrics
                .Include(m => m.Exam)
                .Where(m => m.ExamId == SelectedExamId)
                .OrderBy(m => m.QuestionNumber)
                .ThenBy(m => m.Part)
                .ToListAsync();

            if (!metricsToDownload.Any())
            {
                TempData["ErrorMessage"] = "לא נמצאו פריטים לייצוא עבור בחינה זו.";
                await LoadDataAsync();
                return Page();
            }

            // Get the exam title for the document content
            var examTitle = await _dbContext.Exams
                .Where(e => e.Id == SelectedExamId)
                .Select(e => e.ExamTitle)
                .FirstOrDefaultAsync();

            // Sanitize the filename provided by the user
            var sanitizedFileName = SanitizeFileName(FileName!);
            if (string.IsNullOrEmpty(sanitizedFileName))
            {
                sanitizedFileName = $"מחוון-{string.Join("_", examTitle!.Split(Path.GetInvalidFileNameChars()))}.docx";
            }
            else if (!sanitizedFileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
            {
                sanitizedFileName += ".docx";
            }

            // Generate the Word document
            var memoryStream = DownloadMetrics.GenerateMetricsWordDocument(examTitle!, metricsToDownload);

            // Return the file for download with the sanitized filename
            return File(memoryStream, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", sanitizedFileName);
        }

        private async Task LoadDataAsync()
        {
            var exams = await _dbContext.Exams
                                             .Where(e => e.Active)
                                             .OrderBy(e => e.ExamTitle)
                                             .ToListAsync();

            AvailableExams = new SelectList(exams, "Id", "ExamTitle");

            if (SelectedExamId.HasValue && SelectedExamId.Value > 0)
            {
                SelectedExamTitle = exams.FirstOrDefault(e => e.Id == SelectedExamId.Value)?.ExamTitle;

                Metrics = await _dbContext.Metrics
                    .Include(m => m.Exam)
                    .Where(m => m.ExamId == SelectedExamId)
                    .OrderBy(m => m.QuestionNumber)
                    .ThenBy(m => m.Part)
                    .ToListAsync();

                // Set the default filename
                if (!string.IsNullOrEmpty(SelectedExamTitle))
                {
                    FileName = $"מחוון-{string.Join("_", SelectedExamTitle.Split(Path.GetInvalidFileNameChars()))}.docx";
                }
            }
            else
            {
                SelectedExamTitle = null;
                Metrics = new List<Metric>();
            }
        }

        private string SanitizeFileName(string fileName)
        {
            var invalidChars = new HashSet<char>(Path.GetInvalidFileNameChars());
            var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
            return sanitized;
        }
    }
}
