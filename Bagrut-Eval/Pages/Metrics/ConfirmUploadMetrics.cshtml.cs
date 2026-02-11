using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bagrut_Eval.Models;
using Bagrut_Eval.Data;
using System.Linq;
using System.Security.Claims; // For ClaimTypes
using System; // For DateTime
using System.Text.Json; // For JsonSerializer
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations; // For [Display(Name = "...")]

namespace Bagrut_Eval.Pages.Metrics
{
    public class ConfirmUploadMetricsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        // Properties to hold data for the view (populated OnGet)
        [BindProperty(SupportsGet = true)]
        public int ExamId { get; set; }

        public string? ExamTitle { get; set; }

        // Used to store the parsed new metrics for rendering the view
        public List<Metric> ParsedNewMetrics { get; set; } = new List<Metric>();

        // Bind property for the user's chosen action from the form
        [BindProperty]
        public UploadConflictAction ChosenAction { get; set; }

       [BindProperty(Name = "metricsSessionKey")] // Match name in .cshtml hidden field
        public string MetricsSessionKey { get; set; } = string.Empty;


        public ConfirmUploadMetricsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult OnGet()
        {
            // Retrieve data from TempData on the initial GET request
            // Use 'as string' for safety, then parse numbers.
            string sessionKey = TempData["MetricsSessionKey"]!.ToString()!;
            string examIdString = TempData["ExamIdToConfirm"]!.ToString()!; 
            string examTitle = TempData["ExamTitleToConfirm"]!.ToString()!; 

            int parsedExamId = 0; // Initialize with a default value

            // Check if all necessary TempData values are present and can be parsed correctly
            if (!string.IsNullOrEmpty(sessionKey) &&
                !string.IsNullOrEmpty(examTitle) &&
                int.TryParse(examIdString, out parsedExamId) && // Attempt to parse the string to an int
                parsedExamId != 0) // Ensure the parsed ID is not 0 (which TryParse returns on failure)
            {
                // Now proceed with your existing logic, using the parsed values
                string parsedNewMetricsJson = HttpContext.Session.GetString(sessionKey)!;

                if (string.IsNullOrEmpty(parsedNewMetricsJson))
                {
                    TempData["ErrorMessage"] = "Metrics data expired or not found in session. Please re-upload your file.";
                    return RedirectToPage("/Metrics/UploadMetrics");
                }

                ParsedNewMetrics = JsonSerializer.Deserialize<List<Metric>>(parsedNewMetricsJson)!;
                ExamId = parsedExamId; // Assign the correctly parsed int value
                ExamTitle = examTitle;
                MetricsSessionKey = sessionKey; // Store the key for postback

                // IMPORTANT: DO NOT REMOVE FROM SESSION HERE. It's needed for OnPostAsync.
                // It will be removed after the action in OnPostAsync.
            }
            else // This block will now only be hit if values are truly missing or unparseable
            {
                TempData["ErrorMessage"] = "No metrics data found for confirmation. Please re-upload your file.";
                return RedirectToPage("/Metrics/UploadMetrics");
            }

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Re-deserialize ParsedNewMetrics from the hidden field (ParsedNewMetricsJson)
            if (string.IsNullOrEmpty(MetricsSessionKey))
            {
                TempData["ErrorMessage"] = "Error: Session key for metrics data missing from form submission. Please re-upload your file.";
                return RedirectToPage("/Metrics/UploadMetrics");
            }

            string parsedNewMetricsJson = HttpContext.Session.GetString(MetricsSessionKey)!;

            if (string.IsNullOrEmpty(parsedNewMetricsJson))
            {
                TempData["ErrorMessage"] = "Metrics data expired or not found in session for processing. Please re-upload your file.";
                return RedirectToPage("/Metrics/UploadMetrics");
            }

            try
            {
                ParsedNewMetrics = JsonSerializer.Deserialize<List<Metric>>(parsedNewMetricsJson)!;
            }
            catch (JsonException ex)
            {
                TempData["ErrorMessage"] = $"Error deserializing metrics data from session: {ex.Message}. Please re-upload your file.";
                return RedirectToPage("/Metrics/UploadMetrics");
            }

            if (ParsedNewMetrics == null || !ParsedNewMetrics.Any())
            {
                TempData["ErrorMessage"] = "No new metrics to process. Please re-upload your file.";
                return RedirectToPage("/Metrics/UploadMetrics");
            }

            // Get the current User ID for logging
            int userId = 0;
            var userIdString = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(userIdString) && int.TryParse(userIdString, out int parsedUserId))
            {
                userId = parsedUserId;
            }
            else
            {
                // Log a warning if user ID is not found/parsable, or handle as an error if user must be logged in
                Console.WriteLine("Warning: User ID not found or not parsable for metrics log.");
                TempData["ErrorMessage"] = "Could not identify current user for logging. Please log in.";
                return RedirectToPage("/Metrics/UploadMetrics");
            }


            string successMessage = "";
            string errorMessage = "";

            try
            {
                switch (ChosenAction)
                {
                    case UploadConflictAction.Cancel:
                        successMessage = "Metrics upload cancelled by user.";
                        break;

                    case UploadConflictAction.ReplaceAll:
                        // 1. Delete existing metrics
                        var existingMetricsToDelete = await _context.Metrics
                            .Where(m => m.ExamId == ExamId)
                            .ToListAsync();

                        if (existingMetricsToDelete.Any())
                        {
                            _context.Metrics.RemoveRange(existingMetricsToDelete);
                            await _context.SaveChangesAsync();
                            // Log deletion for each removed metric
                            await LogMetricsDeletion(existingMetricsToDelete, userId);
                        }

                        // 2. Add new metrics
                        _context.Metrics.AddRange(ParsedNewMetrics);
                        await _context.SaveChangesAsync();
                        // Log creation for each new metric
                        await LogMetricsCreation(ParsedNewMetrics, userId);

                        successMessage = $"Replaced all existing metrics for Exam ID {ExamId} with {ParsedNewMetrics.Count} new metrics.";
                        break;

                    case UploadConflictAction.AppendWithoutDuplication:
                        var currentMetrics = await _context.Metrics
                            .AsNoTracking() // Read-only query
                            .Where(m => m.ExamId == ExamId)
                            .ToListAsync();

                        var metricsToAdd = new List<Metric>();
                        var metricsSkipped = 0;

                        foreach (var newMetric in ParsedNewMetrics)
                        {
                            // Define what constitutes an "exact duplicate"
                            // CRITICAL: Adjust this logic based on your definition of uniqueness.
                            // Example: QuestionNumber, Part, RuleDescription, ScoreType as unique key
                            bool isDuplicate = currentMetrics.Any(existing =>
                                existing.QuestionNumber == newMetric.QuestionNumber &&
                                existing.Part == newMetric.Part &&
                                existing.RuleDescription == newMetric.RuleDescription &&
                                existing.ScoreType == newMetric.ScoreType
                            // Consider if Score or Status are part of the uniqueness
                            );

                            if (!isDuplicate)
                            {
                                metricsToAdd.Add(newMetric);
                            }
                            else
                            {
                                metricsSkipped++;
                            }
                        }

                        if (metricsToAdd.Any())
                        {
                            _context.Metrics.AddRange(metricsToAdd);
                            await _context.SaveChangesAsync();
                            await LogMetricsCreation(metricsToAdd, userId);
                            successMessage = $"Appended {metricsToAdd.Count} new unique metrics for Exam ID {ExamId}. Skipped {metricsSkipped} duplicates.";
                        }
                        else
                        {
                            successMessage = $"No new unique metrics to append for Exam ID {ExamId}. Skipped {metricsSkipped} duplicates.";
                        }
                        break;

                    case UploadConflictAction.AppendWithDuplication:
                        _context.Metrics.AddRange(ParsedNewMetrics);
                        await _context.SaveChangesAsync();
                        await LogMetricsCreation(ParsedNewMetrics, userId);
                        successMessage = $"Appended {ParsedNewMetrics.Count} new metrics for Exam ID {ExamId}, allowing duplicates.";
                        break;

                    default:
                        errorMessage = "Invalid action chosen.";
                        break;
                }
            }
            catch (Exception ex)
            {
                errorMessage = $"An error occurred during metrics processing: {ex.Message}";
                // In a real application, you'd also log the full exception details using ILogger
                Console.WriteLine($"Error in ConfirmUploadMetricsModel: {ex.Message}\n{ex.StackTrace}");
            }

            if (!string.IsNullOrEmpty(errorMessage))
            {
                TempData["ErrorMessage"] = errorMessage;
            }
            else
            {
                TempData["SuccessMessage"] = successMessage;
            }

            return RedirectToPage("/Metrics/UploadMetrics"); // Redirect back to the main upload page
        }

        // --- Helper methods for logging ---

        private async Task LogMetricsCreation(List<Metric> metrics, int userId)
        {
            var logEntries = new List<MetricLog>();
            foreach (var metric in metrics)
            {
                logEntries.Add(new MetricLog
                {
                    MetricId = metric.Id,
                    UserId = userId,
                    Date = DateTime.UtcNow,
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

        private async Task LogMetricsDeletion(List<Metric> metrics, int userId)
        {
            var logEntries = new List<MetricLog>();
            foreach (var metric in metrics)
            {
                // For deletions, the log entry captures the state *before* deletion
                logEntries.Add(new MetricLog
                {
                    MetricId = metric.Id,
                    UserId = userId,
                    Date = DateTime.UtcNow,
                    ExamId = metric.ExamId,
                    QuestionNumber = metric.QuestionNumber,
                    Part = metric.Part,
                    RuleDescription = metric.RuleDescription,
                    Score = metric.Score,
                    ScoreType = metric.ScoreType,
                    Status = metric.Status, // The status at the time of deletion
                    Action = "Deleted" // Explicitly log the action
                });
            }
            _context.MetricsLog.AddRange(logEntries);
            await _context.SaveChangesAsync();
        }

        // Enum for the user's choices
        public enum UploadConflictAction
        {
            [Display(Name = "Cancel upload")]
            Cancel,
            [Display(Name = "Replace all existing metrics for this exam")]
            ReplaceAll,
            [Display(Name = "Append new metrics, skipping exact duplicates")]
            AppendWithoutDuplication,
            [Display(Name = "Append all new metrics, allowing duplicates")]
            AppendWithDuplication
        }
    }
}