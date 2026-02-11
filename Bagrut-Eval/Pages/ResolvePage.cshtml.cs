using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Bagrut_Eval.Pages.Common;
using Bagrut_Eval.Utilities;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;

namespace Bagrut_Eval.Pages
{
    [Authorize(Roles = "Senior, Admin")]
    public class ResolveModel : BasePageModel<ResolveModel>
    {
        private readonly IEmailService _emailService;

        [BindProperty(SupportsGet = true)]
        public int IssueId { get; set; }

        [BindProperty(SupportsGet = true)]
        public int? SelectedExamId { get; set; } // To return to the correct exam view on AnswerPage

        public Issue? Issue { get; set; }
        public List<Answer>? Answers { get; set; }

        [BindProperty]
        public Answer NewAnswer { get; set; } = new Answer(); // For adding a new answer

        public int CurrentLoggedInUserId { get; set; }
        public bool HasUserSubmittedAnswer { get; set; }

        public List<User> ActiveSeniors { get; set; } = new List<User>();
        public List<int> InvolvedSeniorIds { get; set; } = new List<int>();

        [BindProperty]
        [ValidateNever]
        public string? MessageContent { get; set; }

        [BindProperty]
        public List<int> SelectedSeniorIds { get; set; } = new List<int>();

        public ResolveModel(ApplicationDbContext context, IEmailService emailService, 
                            ILogger<ResolveModel> logger,ITimeProvider timeProvider) : base(context, logger,timeProvider)
        {
            _emailService = emailService;
        }
        public async Task<IActionResult> OnGetAsync(int issueId, int? selectedExamId)
        {
            await base.OnGetAsync();

            IssueId = issueId;
            SelectedExamId = selectedExamId;
            CurrentLoggedInUserId = GetCurrentUserId();

            if (IssueId == 0)
            {
                TempData["ErrorMessage"] = "Invalid issue ID.";
                return RedirectToPage("/AnswerPage", new { selectedExamId = SelectedExamId });
            }

            await LoadIssueDataAsync();

            if (Issue == null)
            {
                TempData["ErrorMessage"] = "Issue not found.";
                return RedirectToPage("/AnswerPage", new { selectedExamId = SelectedExamId });
            }
            
            ActiveSeniors = await _dbContext.Users
                    // 1. Filter by Active status
                    .Where(u => u.Active && u.UserSubjects.Any(us =>
                                   (us.Role == "Senior" || us.Role == "Admin") && us.SubjectId == SubjectId))
                    .OrderBy(u => u.FirstName)
                    .ToListAsync();

            InvolvedSeniorIds = await _dbContext.Answers
                    .Where(a => a.IssueId == IssueId)
                    .Select(a => a.SeniorId)
                    .Distinct()
                    .ToListAsync();

            var issue = await _dbContext.Issues
                                      .Include(i => i.User)
                                      .FirstOrDefaultAsync(i => i.Id == IssueId);

            var issueOpener = await _dbContext.Users.Include(u => u.UserSubjects).FirstOrDefaultAsync(u => u.Id == issue!.UserId);
            if (issueOpener != null)
            {
                string userRole = issueOpener.UserSubjects.FirstOrDefault()?.Role ?? "Unknown";

                if (userRole == "Senior" && !InvolvedSeniorIds.Contains(issueOpener.Id))
                {
                    InvolvedSeniorIds.Add(issueOpener.Id);
                }
            }           

            return Page();
        }

        private async Task LoadIssueDataAsync()
        {
            Issue = await _dbContext.Issues
                                  .Include(i => i.Exam)
                                  .Include(i => i.Part)
                                  .Include(i => i.User)
                                  .Include(i => i.Drawings) 
                                  .Include(i => i.FinalAnswer) // For highlighting the current final answer
                                  .FirstOrDefaultAsync(i => i.Id == IssueId);

            Answers = await _dbContext.Answers
                                    .Include(a => a.Senior)
                                    .Where(a => a.IssueId == IssueId)
                                    .OrderByDescending(a => a.Date) // Sort by reversed order of dates (newer at the top)
                                    .ToListAsync();
            // Check if the current user has already submitted an answer 
            // This relies on CurrentLoggedInUserId being set by OnGetAsync or OnPostAddAnswerAsync
            HasUserSubmittedAnswer = Answers.Any(a => a.SeniorId == CurrentLoggedInUserId);
        }

        // Handler for adding a new answer
        public async Task<IActionResult> OnPostAddAnswerAsync(int issueId, int? selectedExamId)
        {
            if (ModelState.ContainsKey(nameof(MessageContent)))  // force removal of MessageContent to avoif ModelState error
            {
                ModelState.Remove(nameof(MessageContent));
            }
            // 1. Get the current user ID first, as it's needed for NewAnswer.SeniorId.
            CurrentLoggedInUserId = GetCurrentUserId();

            // 2. Assign the IDs to NewAnswer BEFORE validation.
            NewAnswer.IssueId = issueId;
            NewAnswer.SeniorId = CurrentLoggedInUserId;

            // 3. Clear any ModelState errors related to navigation properties
            // that might have been caused by the initial binding or [Required] attributes on them.
            // This is necessary because you are setting the foreign keys manually.
            ModelState.Remove("NewAnswer.Issue");
            ModelState.Remove("NewAnswer.Senior");

            // 4. Set Page Model properties (for consistent page state).
            IssueId = issueId;
            SelectedExamId = selectedExamId;

            // 5. Set other properties that don't participate in initial model binding but are needed.
            NewAnswer.Date = _timeProvider.Now;

            // 6. Now, perform the validation check.
            if (!ModelState.IsValid)
            {
                // If it's still false here, the issue is with other form-bound properties
                // (like NewAnswer.Content or NewAnswer.Score if their validation fails).
                await LoadIssueDataAsync(); // Reload data to show validation errors
                return Page();
            }

            _dbContext.Answers.Add(NewAnswer);

            // Log this action
            var issueLog = new IssueLog
            {
                IssueId = IssueId,
                UserId = CurrentLoggedInUserId,
                LogDate = _timeProvider.Now,
                Description = $"תשובה חדשה נוספה: '{NewAnswer.Content}' (ציון: {NewAnswer.Score})"
            };
            _dbContext.IssueLogs.Add(issueLog);

            try
            {
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = "התשובה נוספה בהצלחה.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"שגיאה בהוספת התשובה: {ex.Message}";
            }

            return RedirectToPage("/ResolvePage", new { issueId = IssueId, selectedExamId = SelectedExamId });
        }

        // Handler for updating an existing answer (content and/or score)
        public async Task<IActionResult> OnPostUpdateAnswerAsync(int issueId, int answerId, string answerContent, string? answerScore, int? selectedExamId) // Changed answerScore to string?
        {
            IssueId = issueId;
            SelectedExamId = selectedExamId;
            CurrentLoggedInUserId = GetCurrentUserId();

            var answerToUpdate = await _dbContext.Answers.FindAsync(answerId);

            if (answerToUpdate == null)
            {
                TempData["ErrorMessage"] = "תשובה לא נמצאה";
                return RedirectToPage("/ResolvePage", new { issueId = IssueId, selectedExamId = SelectedExamId });
            }

            if (answerToUpdate.SeniorId != CurrentLoggedInUserId && !User.IsInRole("Admin"))
            {
                TempData["ErrorMessage"] = "אינך מורשה לעדכן תשובה זו"; // You are not authorized to update this answer.
                return RedirectToPage("/ResolvePage", new { issueId = IssueId, selectedExamId = SelectedExamId });
            }

            string? oldContent = answerToUpdate.Content;
            string? oldScore = answerToUpdate.Score; // Corrected to string?

            bool changed = false;
            string logDescription = "";

            if (!string.Equals(oldContent, answerContent, StringComparison.Ordinal)) // Compare strings
            {
                answerToUpdate.Content = answerContent;
                changed = true;
                logDescription += $"תוכן עודכן מ '{oldContent}' ל '{answerContent}'. "; // Content updated from 'X' to 'Y'.
            }
            // Compare scores as strings
            if (!string.Equals(oldScore, answerScore, StringComparison.OrdinalIgnoreCase))
            {
                answerToUpdate.Score = answerScore;
                changed = true;
                logDescription += $"ציון עודכן מ '{oldScore}' ל '{answerScore}'. "; // Score updated from 'X' to 'Y'.
            }

            if (changed)
            {
                answerToUpdate.Date = _timeProvider.Now; // Update timestamp
                answerToUpdate.SeniorId = CurrentLoggedInUserId; // Ensure senior ID is current user who updated it

                _dbContext.Entry(answerToUpdate).State = EntityState.Modified;

                // Log this action to IssueLog (removed AnswersLog)
                var issueLog = new IssueLog
                {
                    IssueId = IssueId,
                    UserId = CurrentLoggedInUserId,
                    LogDate = _timeProvider.Now,
                    Description = logDescription.Trim()
                };
                _dbContext.IssueLogs.Add(issueLog);

                try
                {
                    await _dbContext.SaveChangesAsync();
                    TempData["SuccessMessage"] = "התשובה עודכנה בהצלחה."; // Answer updated successfully.
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"שגיאה בעדכון התשובה: {ex.Message}"; // Error updating answer.
                }
            }
            else
            {
                TempData["SuccessMessage"] = "אין שינויים לעדכון בתשובה"; // No changes to update for the answer.
            }

            return RedirectToPage("/ResolvePage", new { issueId = IssueId, selectedExamId = SelectedExamId });
        }

        // Handler for setting/unsetting an answer as FinalAnswer
        public async Task<IActionResult> OnPostSetFinalAnswerAsync(int issueId, int answerId, int? selectedExamId)
        {
            IssueId = issueId;
            SelectedExamId = selectedExamId;
            CurrentLoggedInUserId = GetCurrentUserId();

            var currentIssue = await _dbContext.Issues
                                      .Include(i => i.FinalAnswer) // Include existing FinalAnswer to compare
                                      .FirstOrDefaultAsync(i => i.Id == IssueId);

            if (currentIssue == null)
            {
                TempData["ErrorMessage"] = "Issue not found.";
                return RedirectToPage("/AnswerPage", new { selectedExamId = SelectedExamId });
            }

            string logDescription = "";
            bool statusChanged = false;

            if (currentIssue.FinalAnswerId == answerId)
            {
                currentIssue.FinalAnswerId = null;
                currentIssue.FinalAnswer = null;
                logDescription = "התשובה הסופית בוטלה."; // Final answer deselected.
                statusChanged = true;
            }
            else
            {
                var selectedAnswer = await _dbContext.Answers.FindAsync(answerId);
                if (selectedAnswer == null)
                {
                    TempData["ErrorMessage"] = "Selected answer not found";
                    return RedirectToPage("/ResolvePage", new { issueId = IssueId, selectedExamId = SelectedExamId });
                }

                currentIssue.FinalAnswerId = answerId;
                currentIssue.FinalAnswer = selectedAnswer;
                logDescription = $"תשובה '{selectedAnswer.Content}' נקבעה כסופית."; // Answer 'X' set as final.
                statusChanged = true;
            }

            if (statusChanged)
            {
                _dbContext.Entry(currentIssue).State = EntityState.Modified;

                var issueLog = new IssueLog
                {
                    IssueId = currentIssue.Id,
                    UserId = CurrentLoggedInUserId,
                    LogDate = _timeProvider.Now,
                    Description = logDescription
                };
                _dbContext.IssueLogs.Add(issueLog);

                try
                {
                    await _dbContext.SaveChangesAsync();
                    TempData["SuccessMessage"] = "התשובה הסופית עודכנה בהצלחה."; // Final answer updated successfully.
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"שגיאה בעדכון התשובה הסופית: {ex.Message}"; // Error updating final answer.
                }
            }
            else
            {
                TempData["SuccessMessage"] = "אין שינויים בתשובה הסופית."; // No changes to final answer.
            }

            return RedirectToPage("/ResolvePage", new { issueId = IssueId, selectedExamId = SelectedExamId });
        }

        // Handler for "Update close" button
        public async Task<IActionResult> OnPostUpdateCloseAsync(int issueId, int? selectedExamId)
        {
            IssueId = issueId;
            SelectedExamId = selectedExamId;
            CurrentLoggedInUserId = GetCurrentUserId();

            var issue = await _dbContext.Issues
                                      .Include(i => i.Exam)
                                      .Include(i => i.FinalAnswer)
                                      .Include(i => i.User)
                                      .Include(i => i.Part)
                                            .ThenInclude(p => p!.Exam)
                                      .FirstOrDefaultAsync(i => i.Id == IssueId);

            if (issue == null)
            {
                TempData["ErrorMessage"] = "Issue not found.";
                return RedirectToPage("/AnswerPage", new { selectedExamId = SelectedExamId });
            }

            string logDescription = "";

            if (issue.FinalAnswerId.HasValue) // If a final answer is highlighted
            {
                issue.Status = IssueStatus.Closed; // Set to close
                issue.CloseDate = _timeProvider.Now;
                logDescription = $"השאלה עודכנה ונסגרה עם תשובה סופית: {issue.FinalAnswer!.Content}"; // Issue updated and closed with a final answer.
            }
            else
            {
                issue.Status = IssueStatus.InProgress; // Still open if no final answer set
                issue.CloseDate = null;
                logDescription = "השאלה עודכנה ללא תשובה סופית ונותרה פתוחה."; // Issue updated without final answer and left open.
            }

            _dbContext.Entry(issue).State = EntityState.Modified;

            var issueLog = new IssueLog
            {
                IssueId = issue.Id,
                UserId = CurrentLoggedInUserId,
                LogDate = _timeProvider.Now,
                Description = logDescription
            };
            _dbContext.IssueLogs.Add(issueLog);

            try
            {
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = "השאלה עודכנה בהצלחה."; // Issue updated successfully.
                if (issue.Status == IssueStatus.Closed)  // Sending Email
                {
                    await SendClosedIssueEmailAsync(issue);
                }
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"שגיאה בעדכון השאלה: {ex.Message}"; // Error updating issue.
            }

            return RedirectToPage("/AnswerPage", new { selectedExamId = SelectedExamId });
        }
        private async Task SendClosedIssueEmailAsync(Issue issue)
        {
            if (issue.User == null || string.IsNullOrEmpty(issue.User.Email) || string.IsNullOrEmpty(issue.User.FirstName))
            {
                Console.WriteLine($"Skipping email for Issue {issue.Id}: User or email is missing.");
                return; // Exit the method gracefully
            }

            if (issue.User != null && issue.FinalAnswer != null)
            {
                bool emailSentOk = await _emailService.SendIssueClosedEmailAsync(
                    issue.User.Email,
                    issue.User.FirstName,
                    issue.Exam!.ExamTitle!,
                    issue.Part != null && issue.Part.QuestionNumber != null ? "שאלה " + issue.Part.QuestionNumber : "",
                    issue.Part != null && issue.Part.QuestionPart != null ? "סעיף " + issue.Part.QuestionPart : "",
                    issue.Description!,
                    issue.FinalAnswer.Content!,
                    issue.FinalAnswer.Score != null ? issue.FinalAnswer.Score : "0"
                );
                if (!emailSentOk)
                {
                    TempData["SuccessMessage"] = " **אזהרה: ** האימייל לא נשלח ";
                }
            }
        }

        public async Task<IActionResult> OnPostSendEmailAsync(int issueId, int? selectedExamId)
        {
            IssueId = issueId;
            SelectedExamId = selectedExamId;

            if (SelectedSeniorIds == null || !SelectedSeniorIds.Any())
            {
                TempData["ErrorMessage"] = "נא לבחור מעריך אחד לפחות.";
                return RedirectToPage("/ResolvePage", new { issueId = IssueId, selectedExamId = SelectedExamId });
            }

            var recipients = await _dbContext.Users
                .Where(u => SelectedSeniorIds.Contains(u.Id))
                .ToListAsync();

            var currentSenior = await _dbContext.Users.FindAsync(GetCurrentUserId());
            var issue = await _dbContext.Issues
                .Include(i => i.Exam)
                .Include(i => i.Part)
                .FirstOrDefaultAsync(i => i.Id == IssueId);

            if (currentSenior != null && issue != null)
            {
                await _emailService.SendDiscussionEmailAsync(
                    recipients.Select(u => u.Email!).ToList(),
                    recipients.Select(u => u.FirstName!).ToList(),
                    currentSenior.FirstName!,
                    issue.Exam!.ExamTitle!,
                    issue.Part != null && issue.Part.QuestionNumber != null ? "שאלה " + issue.Part.QuestionNumber : "",
                    issue.Part != null && issue.Part.QuestionPart != null ? "סעיף " + issue.Part.QuestionPart : "",
                    issue.Description!,
                    MessageContent!
                );
                TempData["SuccessMessage"] = "האימייל נשלח בהצלחה למעריכים שנבחרו.";
            }
            else
            {
                TempData["ErrorMessage"] = "שגיאה בשליחת האימייל.";
            }

            return RedirectToPage("/ResolvePage", new { issueId = IssueId, selectedExamId = SelectedExamId });
        }

        // Handler for "Update open" button
        public async Task<IActionResult> OnPostUpdateOpenAsync(int issueId, int? selectedExamId)
        {
            IssueId = issueId;
            SelectedExamId = selectedExamId;
            CurrentLoggedInUserId = GetCurrentUserId();

            var issue = await _dbContext.Issues
                                      .Include(i => i.FinalAnswer)
                                      .FirstOrDefaultAsync(i => i.Id == IssueId);

            if (issue == null)
            {
                TempData["ErrorMessage"] = "Issue not found.";
                return RedirectToPage("/AnswerPage", new { selectedExamId = SelectedExamId });
            }

            string logDescription = "";

            if (issue.FinalAnswerId.HasValue) // If a final answer is highlighted
            {
                // Issue remains open, but note the final answer
                issue.Status = IssueStatus.InProgress;
                issue.CloseDate = null; // Ensure it's not closed
                logDescription = $"עודכן ונותר פתוח עם תשובה סופית: {issue.FinalAnswer!.Content}"; // Issue updated and left open with a final answer.
            }
            else
            {
                // Issue remains open, no final answer set
                issue.Status = IssueStatus.InProgress;
                issue.CloseDate = null;
                logDescription = "עודכן ללא תשובה סופית ונותר פתוח."; // Issue updated without final answer and left open.
            }

            _dbContext.Entry(issue).State = EntityState.Modified;

            var issueLog = new IssueLog
            {
                IssueId = issue.Id,
                UserId = CurrentLoggedInUserId,
                LogDate = _timeProvider.Now,
                Description = logDescription
            };
            _dbContext.IssueLogs.Add(issueLog);

            try
            {
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = "עודכן בהצלחה"; // Issue updated successfully.
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"שגיאה בעדכון: {ex.Message}"; // Error updating issue.
            }

            return RedirectToPage("/AnswerPage", new { selectedExamId = SelectedExamId });
        }

        // Handler for "Cancel" button - simply returns to the answer page
        public IActionResult OnPostCancel(int? selectedExamId)
        {
            SelectedExamId = selectedExamId;
            return RedirectToPage("/AnswerPage", new { selectedExamId = SelectedExamId });
        }

        private int GetCurrentUserId()
        {
            if (User.Identity!.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    return userId;
                }
            }
            return 0;
        }
    }
}