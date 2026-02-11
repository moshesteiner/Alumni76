// File: Pages/Metrics/UploadMetrics.cshtml.cs
using Bagrut_Eval.Data; // Assuming your DbContext is here
using Bagrut_Eval.Models;
using Bagrut_Eval.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Collections.Generic;
using System.Security.Claims; // For ClaimTypes
using System; // For DateTime
using System.Text.Json; // For JsonSerializer
using System.ComponentModel.DataAnnotations;

namespace Bagrut_Eval.Pages.Metrics
{
    //[Authorize(Roles ="Nobody")]    //[Authorize(Roles = "Admin")]
    public class UploadMetricsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        [BindProperty]
        public IFormFile? DocxFile { get; set; }

        [BindProperty]
        public int ExamId { get; set; }

        public List<Exam>? Exams { get; set; }

        public UploadMetricsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task OnGetAsync()
        {
            Exams = await _context.Exams.Where(e => e.Active).ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (DocxFile == null || DocxFile.Length == 0)
            {
                TempData["ErrorMessage"] = "Please select a file to upload.";
                await OnGetAsync();
                return Page();
            }

            var exam = await _context.Exams.FindAsync(ExamId);
            if (exam == null)
            {
                TempData["ErrorMessage"] = "Invalid Exam ID provided. Please select an existing exam.";
                await OnGetAsync();
                return Page();
            }

            // --- Core changes for handling existing metrics ---
            List<Metric> parsedNewMetrics;
            try
            {
                using (var stream = new MemoryStream())
                {
                    await DocxFile.CopyToAsync(stream);
                    stream.Position = 0; // Reset stream position to beginning
                    var parser = new ExamMetricsParser();
                    parser.ParseDocx(stream);
                    parsedNewMetrics = parser.GetMetrics(ExamId);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error parsing the uploaded file: {ex.Message}";
                // In a real application, you'd also log the full exception details
                // e.g., using ILogger<UploadMetricsModel>
                await OnGetAsync();
                return Page();
            }

            var existingMetrics = await _context.Metrics.AsNoTracking().Where(m => m.ExamId == ExamId).ToListAsync();
            if (existingMetrics.Any())
            {
                //// Serialize the parsed new metrics and ExamId to TempData
                //TempData["ParsedNewMetricsJson"] = System.Text.Json.JsonSerializer.Serialize(parsedNewMetrics);
                //TempData["ExamIdToConfirm"] = ExamId;
                //TempData["ExamTitleToConfirm"] = exam.ExamTitle; // Pass exam title for display

                //// Redirect to a new confirmation page
                //return RedirectToPage("/Metrics/ConfirmUploadMetrics"); // Adjust path if ConfirmUploadMetrics is not in /Metrics

                string sessionKey = Guid.NewGuid().ToString(); // Generate a unique key for this data
                HttpContext.Session.SetString(sessionKey, JsonSerializer.Serialize(parsedNewMetrics)); // Store serialized metrics in session

                TempData["MetricsSessionKey"] = sessionKey; // Pass only the key to the next page
                TempData["ExamIdToConfirm"] = ExamId;
                TempData["ExamTitleToConfirm"] = exam.ExamTitle; // Still pass title for display

                return RedirectToPage("/Metrics/ConfirmUploadMetrics");

            }
            else
            {
                // No existing metrics, proceed with direct save and log
                _context.Metrics.AddRange(parsedNewMetrics);
                await _context.SaveChangesAsync();

                // Log the new creations
                await LogMetricsCreation(parsedNewMetrics);

                TempData["SuccessMessage"] = $"Successfully parsed '{DocxFile.FileName}' and saved {parsedNewMetrics.Count} new metrics.";
                return RedirectToPage("/Metrics/UploadMetrics"); // Redirect back to clear the form
            }
        }

        // --- Helper method for logging (moved out for reusability) ---
        private async Task LogMetricsCreation(List<Metric> metrics)
        {
            int userId = 0;
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out int parsedUserId))
            {
                userId = parsedUserId;
            }

            var logEntries = new List<MetricLog>();
            foreach (var metric in metrics) // These metrics now have their Ids populated by EF Core
            {
                logEntries.Add(new MetricLog
                {
                    MetricId = metric.Id,
                    UserId = userId,
                    Date = DateTime.UtcNow, // Use UTC for consistency
                    ExamId = metric.ExamId,
                    QuestionNumber = metric.QuestionNumber,
                    Part = metric.Part,
                    RuleDescription = metric.RuleDescription,
                    Score = metric.Score,
                    ScoreType = metric.ScoreType,
                    Status = metric.Status, // This will be null for new creations from upload
                    Action = "Created" // Explicitly log the action
                });
            }

            _context.MetricsLog.AddRange(logEntries);
            await _context.SaveChangesAsync();
        }
    }
}