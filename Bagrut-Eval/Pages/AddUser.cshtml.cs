// Pages/AddUser.cshtml.cs
using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Bagrut_Eval.Pages.Common;
using Bagrut_Eval.Utilities;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using OfficeOpenXml.Packaging.Ionic.Zlib;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Security.Claims; // Required for User.FindFirst(ClaimTypes.NameIdentifier)
using System.Text;
using System.Threading.Tasks;

namespace Bagrut_Eval.Pages
{
    [Authorize(Roles = "Admin")]
    public class AddUserModel : BasePageModel<AddUserModel>
    {
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEmailService _emailService;

        [BindProperty]
        public User NewUser { get; set; } = new User();

        [BindProperty]
        [Required(ErrorMessage = "נא לבחור תפקיד")]
        public string? NewUserRole { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "סיסמה נדרשת")]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "הסיסמה חייבת להיות באורך של לפחות {2} תווים ובאורך מקסימלי של {1} תווים", MinimumLength = 6)]
        public string? UserPassword { get; set; }
        [BindProperty]
        [Required(ErrorMessage = "נא לבחור מקצוע")]
        public int? SelectedSubjectId { get; set; }
        [BindProperty]
        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>? AvailableSubjects { get; set; }
        public List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>? ExamsForDisplay { get; set; }
        [BindProperty]
        public List<int> SelectedExams { get; set; } = new List<int>();

        [TempData]
        public string Message { get; set; } = string.Empty;

        [BindProperty]
        public IFormFile? BulkAddFile { get; set; }

        public AddUserModel(ApplicationDbContext dbContext, ILogger<AddUserModel> logger, IPasswordHasher<User> passwordHasher,
                                    IEmailService emailService, ITimeProvider timeProvider) : base(dbContext, logger, timeProvider)
        {
            _passwordHasher = passwordHasher;
            _emailService = emailService;
        }

        public new async Task OnGetAsync()
        {
            ModelState.Clear();
            Message = string.Empty;
            await base.OnGetAsync();
            await PopulateViewDataAsync();
            await LoadAvailableExamsAsync();
        }

        public new async Task<IActionResult> OnPostAsync()
        {
            await base.OnPostAsync();

            if (!IsSpecialAdmin)   // "regular" admin
            {
                if (SubjectId == null || SubjectId.Value == 0)
                {
                    Message = "שגיאה: חסר הקשר מקצוע. ודא כי הנך משויך למקצוע";
                    await LoadAvailableExamsAsync();
                    return Page();
                }
            }
            else
            {
                SubjectId = SelectedSubjectId;
                if (SubjectId == null || SubjectId.Value == 0)
                {
                    ModelState.AddModelError("SelectedSubjectId", "נא לבחור מקצוע.");
                    Message = "שגיאה: חובה לבחור מקצוע עבור מעריך חדש.";
                    // Ensure AvailableSubjects is loaded for returning the Page
                    await LoadAvailableSubjectsAsync(); // Assuming this loads the subject list
                    return Page();
                }
            }
            int adminSubjectId = SubjectId!.Value; // The subject the Admin is adding to.
            string assignedRole = NewUserRole!; // The role selected in the form
            string subjectTitle = SubjectTitle ?? "מקצוע לא ידוע";
            UserPassword = GeneratePassword();

            await LoadAvailableExamsAsync();

            //var errorKeys = ModelState.Where(x => x.Value.Errors.Any()).Select(x => x.Key).ToList();

            ModelState.Remove("BulkAddFile");
            ModelState.Remove("SelectedSubjectId");
            ModelState.Remove("UserPassword");
            if (!ModelState.IsValid)
            {
                Message = "נא לתקן את השגיאות בטופס";
                return Page();
            }

            // Normalize Phone Format Before Processing 
            string cleanPhone = new string(NewUser.Phone!.Where(char.IsDigit).ToArray());
            if (cleanPhone.Length == 10) // Standard 05X-XXXXXXX, 07X-XXXXXXX (10 digits)
            {
                NewUser.Phone = $"{cleanPhone.Substring(0, 3)}-{cleanPhone.Substring(3)}";
            }
            else if (cleanPhone.Length == 9) // Standard 0X-XXXXXXX (9 digits)
            {
                NewUser.Phone = $"{cleanPhone.Substring(0, 2)}-{cleanPhone.Substring(2)}";
            }

            // Fetch existing user and their assignments
            var existingUser = await _dbContext.Users  // Include UserSubjects to check for existing assignment
                .Include(u => u.UserSubjects!)
                .Include(u => u.AllowedExams)      // in case such an assignment already exists
                .FirstOrDefaultAsync(u => u.Email == NewUser.Email);

            User userToProcess;
            string successMessage = string.Empty;
            bool isNewUser = existingUser == null; // Flag for logging and email

            if (existingUser != null)    // User Exists ---
            {
                // Check for existing assignment in the current subject (Scenario 2)
                bool alreadyAssigned = existingUser.UserSubjects!.Any(us => us.SubjectId == adminSubjectId);

                if (alreadyAssigned)
                {
                    // ERROR - User already assigned to this subject.
                    TempData["ErrorMessage"] = "האימייל קיים במערכת, וכבר משויך למקצוע זה";
                    await LoadAvailableSubjectsAsync();
                    return Page();
                }

                // User exists but NOT assigned to this subject. Add assignment only.
                userToProcess = existingUser;
                successMessage = $"המעריך '{userToProcess.FirstName} {userToProcess.LastName}' שויך בהצלחה למקצוע חדש!";
                try
                {
                    await _emailService.SendNewAssignmentEmailAsync(userToProcess.Email!, userToProcess.FirstName!,
                                    subjectTitle, RoleDisplayNames[assignedRole]);
                }
                catch (Exception ex)
                {
                    // Log the error
                    _logger.LogError(ex, $"Failed to send new assignment email to {userToProcess.Email}.");
                }
            }
            else  // User does not exist. Create new user. ---
            {
                userToProcess = NewUser;
                userToProcess.PasswordHash = _passwordHasher.HashPassword(userToProcess, UserPassword!);
                _dbContext.Users.Add(userToProcess);

                await _dbContext.SaveChangesAsync();

                successMessage = $"מעריך '{userToProcess.FirstName} {userToProcess.LastName}': ההוספה הושלמה!";
            }

            //  Shared Logic: Add Subject Assignment and Allowed Exams
            //  Add UserSubject assignment for the Admin's subject
            var userSubject = new UserSubject
            {
                UserId = userToProcess.Id,
                SubjectId = adminSubjectId,
                Role = NewUserRole!
            };
            _dbContext.UserSubjects.Add(userSubject);

            // Process AllowedExams (Must use userToProcess.Id for both scenarios)
            if (SelectedExamIds != null && SelectedExamIds.Any())
            {
                var existingAllowedExamIds = userToProcess.AllowedExams?.Select(ae => ae.ExamId).ToHashSet() ?? new HashSet<int>();
                var examsToAddNewly = SelectedExamIds.Where(examId => !existingAllowedExamIds.Contains(examId)).ToList();
                foreach (var examId in examsToAddNewly)
                {
                    var allowedExam = new AllowedExam
                    {
                        UserId = userToProcess.Id,
                        ExamId = examId
                    };
                    _dbContext.AllowedExams.Add(allowedExam);
                }
            }

            // Save all changes (UserSubject and AllowedExams)
            await _dbContext.SaveChangesAsync();

            foreach (var examId in SelectedExams)
            {
                var allowedExam = new AllowedExam
                {
                    UserId = NewUser.Id, // Use the new user's ID
                    ExamId = examId
                };
                _dbContext.AllowedExams.Add(allowedExam);
            }

            // Prepare UserLog for both scenarios
            var selectedExamTitles = await _dbContext.Exams
                                                     .Where(e => SelectedExamIds!.Contains(e.Id))
                                                     .Select(e => e.ExamTitle)
                                                     .ToListAsync();

            string actionText = isNewUser ? "מעריך חדש נוסף" : "מעריך קיים שויך למקצוע חדש";
            successMessage = isNewUser ? "מעריך חדש נוסף" : "המעריך שויך למקצוע חדש";
            var descriptionForLog = $"{actionText}: {userToProcess.FirstName} {userToProcess.LastName}, {userToProcess.Email}, מקצוע: {subjectTitle}, תפקיד: {assignedRole}, בחינות: {string.Join(", ", selectedExamTitles)}";

            // Get the InitiatorId
            var initiatorIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? initiatorId = null;
            if (initiatorIdString != null && int.TryParse(initiatorIdString, out int parsedId))
            {
                initiatorId = parsedId;
            }

            // Create and add the UserLog entry
            var newUserLog = new UserLog
            {
                UserId = userToProcess.Id,
                InitiatorId = initiatorId,
                Date = DateTime.UtcNow,
                Description = descriptionForLog
            };

            _dbContext.UsersLog.Add(newUserLog);
            await _dbContext.SaveChangesAsync(); // Save the UserLog entry
            if (isNewUser)
            {
                try
                {
                    await _emailService.SendWelcomeEmailAsync(userToProcess.Email!, userToProcess.FirstName!, UserPassword!, subjectTitle);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to send welcome email to new user {userToProcess.Email}.");
                }
            }

            //  Finalization
            TempData["SuccessMessage"] = successMessage;

            // Clear form for new entry
            NewUser = new User { Active = true };
            NewUserRole = null;
            UserPassword = null;
            SelectedExamIds = new List<int>();
            await LoadAvailableExamsAsync();

            // Redirect to the GET handler to clear the form POST state and refresh the page
            return RedirectToPage();
        }

        private async Task LoadAvailableSubjectsAsync()
        {
            AvailableSubjects = await _dbContext.Subjects
                .Where(s => s.Id > 1)   // ignore place holder subject
                .OrderBy(s => s.Title)
                .Select(s => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.Title
                })
                .ToListAsync();
        }

        private async Task PopulateViewDataAsync()
        {
            if (IsSpecialAdmin)
            {
                await LoadAvailableSubjectsAsync();
                ExamsForDisplay = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
            }

            // Load Exams for Regular Admin (only if not SpecialAdmin and SubjectId is set)
            if (!IsSpecialAdmin && SubjectId.HasValue)
            {
                ExamsForDisplay = await _dbContext.Exams
                    .Where(e => e.Active && e.SubjectId == SubjectId.Value)
                    .OrderBy(e => e.ExamTitle)
                    .Select(e => new Microsoft.AspNetCore.Mvc.Rendering.SelectListItem
                    {
                        Value = e.Id.ToString(),
                        Text = e.ExamTitle
                    })
                    .ToListAsync();
            }
            else if (!IsSpecialAdmin && !SubjectId.HasValue)
            {
                ExamsForDisplay = new List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem>();
            }
        }
        private async Task LoadAvailableExamsAsync()
        {
            LoadAdminContext();
            AvailableExamsList = await _dbContext.Exams
                .Where(e => e.Active)
                .Where(e => e.SubjectId == SubjectId)
                .OrderBy(e => e.ExamTitle)
                .ToListAsync();
        }

        public async Task<IActionResult> OnPostBulkAddAsync()
        {
            await base.OnPostAsync(); // Load Admin context

            // 0. Pre-Checks
            if (SubjectId == null || SubjectId.Value == 0)
            {
                TempData["ErrorMessage"] = "שגיאה: חסר הקשר מקצוע. ודא כי הנך משויך למקצוע";
                return RedirectToPage();
            }
            if (BulkAddFile == null || BulkAddFile.Length == 0)
            {
                TempData["ErrorMessage"] = "נא לבחור קובץ אקסל";
                await LoadAvailableExamsAsync();
                return Page();
            }

            // 1. Initial Setup and Pre-loading
            int adminSubjectId = SubjectId.Value;
            string subjectTitle = SubjectTitle ?? "מקצוע לא ידוע";

            var successfullyAddedUsers = new List<(string name, string email, string password)>();
            var notAddedUsers = new List<string>();
            var bulkUsersForLog = new List<string>();

            // Pre-load all necessary DB data efficiently
            var subjectExams = await _dbContext.Exams
                                               .Where(e => e.SubjectId == adminSubjectId && e.Active)
                                               .ToDictionaryAsync(e => e.ExamTitle!, e => e.Id);

            var existingUsersMap = await _dbContext.Users
                .Include(u => u.UserSubjects!)
                .Include(u => u.AllowedExams!)
                .ToDictionaryAsync(u => u.Email!, u => u);

            try
            {
                //  Excel Parsing and Row Processing Loop
                OfficeOpenXml.ExcelPackage.License.SetNonCommercialPersonal("Moshe Steiner");
                using (var stream = new MemoryStream())
                {
                    await BulkAddFile.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets.First();
                        int rowCount = worksheet.Dimension.Rows;

                        for (int row = 2; row <= rowCount; row++)
                        {
                            var rowData = ReadRowData(worksheet, row);
                            if (rowData == null)
                            {
                                notAddedUsers.Add($"שורה {row}: חסר פרט חובה (שם, דוא\"ל, תפקיד)");
                                continue;
                            }

                            var (success, logEntry, newPassword) = await ProcessSingleUserRowAsync(
                                rowData, adminSubjectId, subjectTitle, subjectExams, existingUsersMap, notAddedUsers);

                            if (success)
                            {
                                bulkUsersForLog.Add(logEntry);
                                if (!string.IsNullOrEmpty(newPassword))
                                {
                                    successfullyAddedUsers.Add((rowData.FirstName, rowData.Email, newPassword));
                                }
                            }
                        }
                    }
                }

                // 3. Finalization and Logging
                return await LogAndFinalizeBulkOperationAsync(successfullyAddedUsers, notAddedUsers, bulkUsersForLog);
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"שגיאה חריגה בתהליך הוספה גורפת: {ex.Message}";
                _logger.LogError(ex, "Bulk add operation failed with unhandled exception.");
            }

            await LoadAvailableExamsAsync();
            return RedirectToPage();
        }

        // ----------------------------------------------------------------------------------
        // --- PRIVATE HELPER METHODS FOR BULK ADD ---
        // ----------------------------------------------------------------------------------
        private class BulkUserRowData
        {
            public int RowNumber { get; set; }
            public string FirstName { get; set; } = string.Empty;
            public string LastName { get; set; } = string.Empty;
            public string Email { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public List<string> ExamTitles { get; set; } = new List<string>();
        }

        private BulkUserRowData? ReadRowData(ExcelWorksheet worksheet, int row)
        {
            // Read columns (assuming 1-based index)
            string? firstName = worksheet.Cells[row, 1].GetValue<string>()?.Trim();
            string? lastName = worksheet.Cells[row, 2].GetValue<string>()?.Trim();
            string? email = worksheet.Cells[row, 3].GetValue<string>()?.Trim();
            string? phone = worksheet.Cells[row, 4].GetValue<string>()?.Trim();
            string? role = worksheet.Cells[row, 5].GetValue<string>()?.Trim();
            string? exam1Title = worksheet.Cells[row, 6].GetValue<string>()?.Trim();
            string? exam2Title = worksheet.Cells[row, 7].GetValue<string>()?.Trim();

            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(firstName) || string.IsNullOrEmpty(lastName) || string.IsNullOrEmpty(role))
            {
                return null;
            }

            var examTitles = new List<string?> { exam1Title, exam2Title }.Where(t => !string.IsNullOrEmpty(t)).ToList()!;

            return new BulkUserRowData
            {
                RowNumber = row,
                FirstName = firstName!,
                LastName = lastName!,
                Email = email!,
                Phone = phone!,
                Role = role!,
                ExamTitles = examTitles!
            };
        }

        private async Task<(bool success, string logEntry, string? newPassword)> ProcessSingleUserRowAsync(
                                BulkUserRowData rowData,
                                int adminSubjectId,
                                string subjectTitle,
                                Dictionary<string, int> subjectExams,
                                Dictionary<string, User> existingUsersMap,
                                List<string> notAddedUsers)
        {
            // Basic validation
            if (!IsValidRole(rowData.Role)) // Assumes a utility method/enum for role validation
            {
                notAddedUsers.Add($"שורה {rowData.RowNumber} ({rowData.Email}): תפקיד לא חוקי ({rowData.Role})");
                return (false, string.Empty, null);
            }

            User userToProcess;
            string generatedPassword = string.Empty;
            bool isNewUser = false;

            if (existingUsersMap.TryGetValue(rowData.Email, out User? existingUser))
            {
                // 2. User exists: Check for existing assignment
                userToProcess = existingUser;
                bool alreadyAssigned = existingUser.UserSubjects!.Any(us => us.SubjectId == adminSubjectId);

                if (alreadyAssigned)
                {
                    notAddedUsers.Add($"שורה {rowData.RowNumber} ({rowData.Email}): המעריך כבר משויך למקצוע {subjectTitle}.");
                    return (false, string.Empty, null);
                }
            }
            else   // New user: Create and save to get the Id
            {
                isNewUser = true;

                generatedPassword = GeneratePassword();

                userToProcess = new User
                {
                    FirstName = rowData.FirstName,
                    LastName = rowData.LastName,
                    Email = rowData.Email,
                    Phone = rowData.Phone,
                    Active = true,
                    PasswordHash = _passwordHasher.HashPassword(null!, generatedPassword)
                };
                _dbContext.Users.Add(userToProcess);
                await _dbContext.SaveChangesAsync();

                existingUsersMap.Add(userToProcess.Email!, userToProcess);
            }

            // --- Subject Assignment ---
            var userSubject = new UserSubject
            {
                UserId = userToProcess.Id,
                SubjectId = adminSubjectId,
                Role = rowData.Role
            };

            _dbContext.UserSubjects.Add(userSubject);

            if (userToProcess.UserSubjects == null)
            {
                // This should not happen if eagerly loaded, but this defensive check is safe.
                userToProcess.UserSubjects = new List<UserSubject>();
            }
            userToProcess.UserSubjects.Add(userSubject);

            // --- Exam Processing (Migration Plan Step 3) ---
            var examsAddedTitles = new List<string>();
            var existingAllowedExamIds = userToProcess.AllowedExams?.Select(ae => ae.ExamId).ToHashSet() ?? new HashSet<int>();

            foreach (var title in rowData.ExamTitles)
            {
                if (subjectExams.TryGetValue(title, out int examId))
                {
                    if (!existingAllowedExamIds.Contains(examId))
                    {
                        var allowedExam = new AllowedExam { UserId = userToProcess.Id, ExamId = examId };
                        _dbContext.AllowedExams.Add(allowedExam);
                        examsAddedTitles.Add(title);
                        existingAllowedExamIds.Add(examId);
                    }
                }
            }

            // --- Log Entry Creation ---
            string assignmentStatus = isNewUser ? "נוצר" : "שויך מחדש";
            string logEntry = $"{userToProcess.FirstName} {userToProcess.LastName} ({userToProcess.Email}) - {assignmentStatus} למקצוע {subjectTitle} ({rowData.Role}). בחינות: {(examsAddedTitles.Any() ? string.Join(", ", examsAddedTitles) : "ללא")}.";

            // Return success status, the log entry, and the password (if new user)
            return (true, logEntry, isNewUser ? generatedPassword : null);
        }
        private string GeneratePassword()
        {
            return "TempP@ss" + Guid.NewGuid().ToString().Substring(0, 5);
        }

        private async Task<IActionResult> LogAndFinalizeBulkOperationAsync(
            List<(string name, string email, string password)> successfullyAddedUsers,
            List<string> notAddedUsers,
            List<string> bulkUsersForLog)
        {
            // Commit all pending UserSubjects and AllowedExams changes from the loop
            await _dbContext.SaveChangesAsync();

            // --- Logging the Bulk Operation for the Admin ---
            var logMessage = $"הוספה גורפת הושלמה. סה\"כ הוספו/שוייכו: {bulkUsersForLog.Count} מעריכים. שורות כשל: {notAddedUsers.Count}.";

            var initiatorIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int initiatorId = 0;
            if (initiatorIdString != null && int.TryParse(initiatorIdString, out int parsedId))
            {
                initiatorId = parsedId;
            }

            var bulkLog = new UserLog
            {
                UserId = initiatorId,
                InitiatorId = initiatorId,
                Date = DateTime.UtcNow,
                Description = $"{logMessage}<br />--- פרטים ---<br />{ListToString(bulkUsersForLog)}<br />--- כשלונות ---<br />{ListToString(notAddedUsers)}"
            };

            _dbContext.UsersLog.Add(bulkLog);
            await _dbContext.SaveChangesAsync(); // Save the log entry

            // --- Final Notification ---
            await SendEmailToLoggedUser(successfullyAddedUsers, notAddedUsers);

            // --- Finalization ---
            TempData["SuccessMessage"] = logMessage;
            TempData["BulkUsers"] = ListToString(successfullyAddedUsers);

            await LoadAvailableExamsAsync();
            return RedirectToPage();
        }

        private async Task SendEmailToLoggedUser(List<(string, string, string)> added, List<string> notAdded)
        {
            if (User.Identity!.IsAuthenticated)
            {
                var userEmail = User.FindFirst(ClaimTypes.Email)?.Value;
                var userFirstName = User.FindFirst(ClaimTypes.GivenName)?.Value;
                if (!string.IsNullOrEmpty(userEmail) && !string.IsNullOrEmpty(userFirstName))
                {
                    await _emailService.SendAddBulkEmailAsync(userEmail, userFirstName, ListToString(added), ListToString(notAdded));
                }
            }
        }
        private string ListToString(List<(string name, string email, string password)> list)
        {
            string str = "";
            foreach (var user in list)
            {
                str += $"שם: {user.name}, דוא\"ל: {user.email}, סיסמה: {user.password}<br />";
            }
            return str;
        }
        private string ListToString(List<string> list)
        {
            string str = "";
            foreach (var user in list)
            {
                str += $"{user}<br />";
            }
            return str;
        }
    }
}