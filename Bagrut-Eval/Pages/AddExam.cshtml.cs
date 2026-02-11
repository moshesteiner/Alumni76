using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Bagrut_Eval.Pages.Common;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Bagrut_Eval.Pages
{
    [Authorize(Roles = "Admin")]
    public class AddExamModel : BasePageModel<AddExamModel>
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        public IList<Exam> Exams { get; set; }

        [BindProperty(SupportsGet = true)]
        public Exam NewExam { get; set; }       

        public AddExamModel(ApplicationDbContext dbContext,
                            ILogger<AddExamModel> logger,
                            IHttpContextAccessor httpContextAccessor,
                            ITimeProvider timeProvider):base(dbContext, logger, timeProvider) 
        {
            _httpContextAccessor = httpContextAccessor;
            Exams = new List<Exam>();
            NewExam = new Exam();
        }
        private async Task LoadExamsAsync()
        {
            if (SubjectId.HasValue)
            {
                Exams = await _dbContext.Exams
                                         .Where(e => e.SubjectId == SubjectId)
                                         .OrderByDescending(e => e.Active)
                                         .ThenBy(e => e.ExamTitle)
                                         .ToListAsync();
            }
            else
            {
                // Should not happen if OnGetAsync checks are correct, but safe fallback
                Exams = new List<Exam>();
                _logger.LogWarning("LoadExamsAsync called without a valid SubjectId.");
            }
        }

        public new async Task<IActionResult> OnGetAsync()
        {
            await base.OnGetAsync();
            //(SubjectId, SubjectTitle) = GetUserSubject();
            LoadAdminContext();
            if (IsSpecialAdmin)
                return RedirectToPage("/Index");    // avoid Special Admin this page - as SubjectId is not set
            await LoadExamsAsync();
            //NewExam = new Exam();      // new Exam { Active = false };
            return Page();               // Load Exams, now filtered by the subject
        }

        public async Task<IActionResult> OnGetEditAsync(int id)
        {
            (SubjectId, SubjectTitle) = GetUserSubject();
            await LoadExamsAsync();
            var examToEdit = await _dbContext.Exams.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);

            if (examToEdit == null)
            {
                _logger.LogWarning("Attempted to edit non-existent exam with Id: {Id}", id);
                TempData["ErrorMessage"] = "הבחינה לא נמצאה לעריכה.";
                return RedirectToPage();
            }

            NewExam = examToEdit;
            return Page();
        }

        // --- Modified OnPostAsync Method for Logging and Correct Update ---
        public new async Task<IActionResult> OnPostAsync()
        {
            await base.OnPostAsync();

            var formExam = NewExam;
            (SubjectId, SubjectTitle) = GetUserSubject();
            if (!SubjectId.HasValue)
            {
                // Guard check for missing claims (security/robustness)
                TempData["ErrorMessage"] = "שגיאה: פרטי המקצוע חסרים. נסה להתחבר מחדש.";
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                return RedirectToPage("/Index");
            }

            if (!await TryUpdateModelAsync(
                formExam,
                "NewExam",
                e => e.ExamTitle,
                e => e.Active,
                e => e.IsLocked))
            {
                _logger.LogWarning("Exam form submission failed due to validation or binding errors.");
                await LogModelStateErrors();
                await LoadExamsAsync();
                return Page();
            }
            int? currentUserId = GetCurrentUserId();
            if (currentUserId == null) // Check for null before attempting to use .Value
            {
                _logger.LogError("User ID not found for logging exam event.");
                TempData["ErrorMessage"] = "שגיאה: לא ניתן לזהות משתמש לרישום פעילות.";
                await LoadExamsAsync();
                return Page();
            }
            // Check for Title Duplication (Recommended check for the specific subject)
            var existingExam = await _dbContext.Exams
                         .Where(e => e.SubjectId == SubjectId!.Value)
                         .Where(e => e.ExamTitle == formExam.ExamTitle)
                         .Where(e => e.Id != formExam.Id) //  Exclude the current exam ID (crucial for updates)
                         .FirstOrDefaultAsync();
            if (existingExam != null)
            {
                ModelState.AddModelError("NewExam.ExamTitle", "שם הבחינה כבר קיים במקצוע זה.");
                TempData["ErrorMessage"] = "שם הבחינה כבר קיים במקצוע זה.";
                await LoadExamsAsync();
                return Page();
            }

           
            if (formExam.Id == 0) // It's a new exam
            {
                formExam.SubjectId = SubjectId.Value;   // adding SubjectId
                _dbContext.Exams.Add(formExam);
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("New exam '{ExamTitle}' added successfully. SubjectId: {SubjectId}", formExam.ExamTitle, formExam.SubjectId);
                TempData["SuccessMessage"] = "הבחינה נוספה בהצלחה!";

                // Log New Exam Event
                await LogExamEvent(formExam.Id, currentUserId!.Value,
                                    $"בחינה חדשה: {formExam.ExamTitle}, סטטוס: {(formExam.Active ? "פעיל" : "לא פעיל")}, מקצוע: {SubjectTitle}");
            }           
            else // It's an existing exam to update
            {
                 var examToUpdate = await _dbContext.Exams.Where(e => e.SubjectId == SubjectId!.Value)
                                                       .FirstOrDefaultAsync(e => e.Id == formExam.Id);

                if (examToUpdate == null)
                {
                    _logger.LogError("Attempted to update non-existent exam with Id: {Id} or exam not in SubjectId: {SubjectId}", formExam.Id, SubjectId!.Value);
                    TempData["ErrorMessage"] = "שגיאה: הבחינה לעדכון לא נמצאה או לא שייכת למקצוע הנבחר.";
                    await LoadExamsAsync();
                    return Page();
                }

                // Capture original values for logging BEFORE updating
                string originalExamTitle = examToUpdate.ExamTitle!;
                bool originalActiveStatus = examToUpdate.Active;
                bool originalIsLockedStatus = examToUpdate.IsLocked;

                // *** CRITICAL CHANGE: Directly assign updated values to the tracked entity ***
                examToUpdate.ExamTitle = formExam.ExamTitle;
                examToUpdate.Active = formExam.Active;
                examToUpdate.IsLocked = formExam.IsLocked;

                // Save changes to the database. EF Core will now detect the modifications.
                await _dbContext.SaveChangesAsync();

                // Check for changes to log AFTER saving
                string logDescription = "";
                bool changesMade = false;

                if (originalExamTitle != formExam.ExamTitle)
                {
                    logDescription += $"שינוי שם הבחינה מ-'{originalExamTitle}' ל-'{formExam.ExamTitle}'. ";
                    changesMade = true;
                }

                if (originalActiveStatus != formExam.Active)
                {
                    logDescription += $"שינוי סטטוס: מ-{(originalActiveStatus ? "פעיל" : "לא פעיל")} ל-{(formExam.Active ? "פעיל" : "לא פעיל")}";
                    changesMade = true;
                }
                if (originalIsLockedStatus != formExam.IsLocked) 
                {
                    if (changesMade) logDescription += "; "; // Add separator if another change was logged
                    logDescription += $"שינוי נעילה: מ-{(originalIsLockedStatus ? "נעול" : "פתוח")} ל-{(formExam.IsLocked ? "נעול" : "פתוח")}";
                    changesMade = true;
                }

                if (changesMade)
                {
                    await LogExamEvent(formExam.Id, currentUserId.Value, $"{logDescription.Trim()}");
                }
                else
                {
                    _logger.LogInformation("Exam '{ExamTitle}' (Id: {Id}) updated, but no changes detected for logging.", formExam.ExamTitle, formExam.Id);
                }

                _logger.LogInformation("Exam '{ExamTitle}' (Id: {Id}) updated successfully.", formExam.ExamTitle, formExam.Id);
                TempData["SuccessMessage"] = "בחינה עודכנה בהצלחה!";
            }

            return RedirectToPage("/AddExam");
        }

        private async Task LogModelStateErrors()
        {
            foreach (var modelStateEntry in ModelState)
            {
                var propertyName = modelStateEntry.Key;
                var errors = modelStateEntry.Value.Errors;

                if (errors.Any())
                {
                    foreach (var error in errors)
                    {
                        _logger.LogError("ModelState Error for property '{PropertyName}': {ErrorMessage}", propertyName, error.ErrorMessage);
                    }
                }
            }
            await Task.CompletedTask;
        }

        private async Task LogExamEvent(int examId, int userId, string description)
        {
            var examLog = new ExamLog
            {
                ExamId = examId,
                Date = _timeProvider.Now,
                UserId = userId,
                Description = description
            };

            _dbContext.ExamsLog.Add(examLog);
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("ExamLog created for ExamId: {ExamId}, UserId: {UserId}, Description: {Description}", examId, userId, description);
        }

        private int? GetCurrentUserId()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && int.TryParse(userIdClaim, out int userId))
            {
                return userId;
            }
            return null;
        }
    }
}