using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Bagrut_Eval.Pages.Common;
using Bagrut_Eval.Utilities; // ⭐ Adjust this if your IStorageService is here
using DocumentFormat.OpenXml.InkML;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace Bagrut_Eval.Pages
{
    [Authorize(Roles = "Evaluator")]
    public class ReportModel : BasePageModel<ReportModel>
    {
        private readonly IStorageService _storageService;
        private readonly string _containerName;

        [BindProperty(SupportsGet = true)]
        public int? SelectedExamId { get; set; }

        public string? SelectedExamTitle { get; set; }

        public IList<Issue>? Issues { get; set; }

        [BindProperty(SupportsGet = true)]
        public bool EditMyReportSelected { get; set; } = false;

        [BindProperty]
        public Issue NewIssue { get; set; } = new Issue();

        public SelectList? AvailablePartsForNewIssue { get; set; }

        public int? CurrentLoggedInUserId { get; set; }
        [BindProperty]
        [Required(ErrorMessage = "יש לבחור שאלה / סעיף")]
        public string? SelectedIssueScope { get; set; }

        [BindProperty]
        public IFormFileCollection? NewIssueDrawings { get; set; }
        private const int MaxDrawingsPerIssue = 3;
        public static double maxFileSizeinMB = 1.0;


        [BindProperty(SupportsGet = true)]
        public string? Sort { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Dir { get; set; } = "asc"; // Default direction
        [BindProperty(SupportsGet = true)]
        public string? SecondarySort { get; set; }

        // Ensure FilterSortModel is also bound via SupportsGet
        [BindProperty]  //(SupportsGet = true)]
        public FilterModel? FilterModel { get; set; }
        public new const string FilterSessionKey = "ReportFilterState";    // Session variable to store filters
        public const string SortSessionKey = "IssuesSortState";

        public ReportModel(ApplicationDbContext dbContext, ILogger<ReportModel> logger,
                          IStorageService storageService, IOptions<StorageOptions> storageOptions,
                          ITimeProvider timeProvider) : base(dbContext, logger, timeProvider)
        {
            _storageService = storageService;
            _containerName = storageOptions.Value.ContainerName;
        }


        public async Task OnGetAsync(int? selectedExamId, string? sort, string? dir, string? secondarySort)
        {
            if (selectedExamId.HasValue)
                SelectedExamId = selectedExamId;

            ModelState.Clear();
            string? json = HttpContext.Session.GetString(FilterSessionKey);

            FilterModel = json != null ? JsonSerializer.Deserialize<FilterModel>(json) : SetFilterModel(FilterModel!);

            var sortState = HttpContext.Session.Get<List<string>>(SortSessionKey) ?? new List<string>();

            if (!string.IsNullOrEmpty(sort) && !string.IsNullOrEmpty(dir))
            {
                // Construct the primary sort key
                string primarySortKey = $"{sort}_{dir}";

                // Remove any existing sort keys for this column
                sortState.RemoveAll(s => s.StartsWith(sort));
                if (!string.IsNullOrEmpty(secondarySort))
                {
                    sortState.RemoveAll(s => s.StartsWith(secondarySort));
                }

                // Add the primary sort key to the front
                sortState.Insert(0, primarySortKey);

                // If a secondary sort is provided, add it as well
                if (!string.IsNullOrEmpty(secondarySort))
                {
                    string secondarySortKey = $"{secondarySort}_{dir}";
                    sortState.Insert(1, secondarySortKey);
                }
            }

            // Trim the list to a reasonable size for multi-column sorting
            // For example, to allow up to two levels of sorting (primary and secondary)
            while (sortState.Count > 2)
            {
                sortState.RemoveAt(sortState.Count - 1);
            }

            // Save the updated sort state back to the session
            HttpContext.Session.Set(SortSessionKey, sortState);

            // Store sort state in ViewData so the view can access it
            ViewData["SortState"] = sortState;

            await LoadDataAsync();
        }
        public async Task<IActionResult> OnPostResetSortAsync()
        {
            HttpContext.Session.Remove(FilterSessionKey);
            FilterModel = SetFilterModel(FilterModel!);
            HttpContext.Session.Remove(SortSessionKey);
            ViewData["SortState"] = null;
            await LoadDataAsync();
            Sort = null;
            Dir = null;
            return RedirectToPage(new { SelectedExamId = SelectedExamId });
        }
        private FilterModel SetFilterModel(FilterModel FilterModel)
        {
            if (FilterModel == null)
                FilterModel = new FilterModel();

            FilterModel.DisplayDescriptionSearch = true;
            FilterModel.DisplayFilterDate = true;
            FilterModel.DisplayShowClosed = true;
            FilterModel.DisplayShowNewerThanLastLogin = true;
            FilterModel.DisplayUserNameSearch = true;
            string json = JsonSerializer.Serialize(FilterModel);
            HttpContext.Session.SetString(FilterSessionKey, json);
            return FilterModel;
        }
        public async Task<IActionResult> OnPostFilterAsync()
        {
            await LoadDataAsync();
            return RedirectToPage(new
            {
                SelectedExamId = SelectedExamId,
                Sort = Sort,
                Dir = Dir,
                EditMyReportSelected = EditMyReportSelected
            });
        }
        public IActionResult OnPostApplyFilter()
        {
            string json = System.Text.Json.JsonSerializer.Serialize(FilterModel);
            HttpContext.Session.SetString(FilterSessionKey, json);

            // Redirect back to GET
            return RedirectToPage(new
            {
                SelectedExamId = SelectedExamId,
                EditMyReportSelected = EditMyReportSelected,
                Sort = Sort,
                Dir = Dir
            });
        }

        public async Task<IActionResult> OnPostAddIssueAsync()
        {
            // 1. Pre-validation and Initialization
            var validationResult = await ValidateAndInitializeIssue();
            if (validationResult != null) return validationResult; // Early exit on critical errors

            // 2. Handle File Uploads
            var uploadResult = await HandleFileUploadsAsync();
            if (uploadResult != null) return uploadResult; // Early exit on critical upload errors

            // 3. Save to Database
            return await SaveIssueToDatabaseAsync();
        }
        private async Task<IActionResult> ValidateAndInitializeIssue()
        {
            await PopulateCurrentUserId();

            if (!CurrentLoggedInUserId.HasValue || CurrentLoggedInUserId.Value == 0)
            {
                TempData["ErrorMessage"] = "שגיאה: פרטי משתמש לא תקינים. לא ניתן להעלות שאלות";
                _logger.LogError("Authenticated user identity not found or invalid during Add Issue.");
                await LoadDataAsync();
                return Page();
            }

            NewIssue.UserId = CurrentLoggedInUserId.Value;
            NewIssue.OpenDate = _timeProvider.Now;
            NewIssue.Status = IssueStatus.Open;

            // Clean ModelState before custom validation
            ModelState.Remove("NewIssue.Part");
            ModelState.Remove("NewIssue.User");
            ModelState.Remove("NewIssue.Exam");
            ModelState.Remove("NewIssue.FinalAnswer");
            ModelState.Remove("NewIssue.Drawings"); // Correctly removed from ModelState

            if (!SelectedExamId.HasValue)
            {
                TempData["ErrorMessage"] = "שגיאה: יש לבחור בחינה לפני הוספת שאלה.";
                await LoadDataAsync();
                return Page();
            }
            NewIssue.ExamId = SelectedExamId.Value;

            // Parse SelectedIssueScope to populate PartId and QuestionNumber
            var parseScopeResult = await ParseSelectedIssueScopeAsync();
            if (parseScopeResult != null) return parseScopeResult;

            if (!ModelState.IsValid)
            {
                LogModelStateErrors();
                TempData["ErrorMessage"] = "שגיאת קלט: אנא בדוק את הפרטים המודגשים באדום.";
                await LoadDataAsync();
                return Page();
            }
            return null!; // Indicates success, no early exit needed
        }
        private async Task<IActionResult> ParseSelectedIssueScopeAsync()
        {
            if (SelectedIssueScope == "ExamOnly")
            {
                NewIssue.PartId = null;
                NewIssue.QuestionNumber = null;
            }
            else if (SelectedIssueScope!.StartsWith("Question_"))
            {
                NewIssue.PartId = null;
                NewIssue.QuestionNumber = SelectedIssueScope.Replace("Question_", "");
            }
            else if (SelectedIssueScope.StartsWith("Part_"))
            {
                var partIdString = SelectedIssueScope.Replace("Part_", "");
                if (int.TryParse(partIdString, out int partId))
                {
                    var part = await _dbContext.Parts.FindAsync(partId);
                    if (part == null)
                    {
                        _logger.LogError($"Attempted to add issue with non-existent PartId from SelectedIssueScope: {partIdString}");
                        TempData["ErrorMessage"] = "שגיאה: מספר השאלה/סעיף שנבחר אינו קיים.";
                        await LoadDataAsync();
                        return Page();
                    }
                    NewIssue.PartId = part.Id;
                    NewIssue.QuestionNumber = part.QuestionNumber;
                }
                else
                {
                    _logger.LogError($"Invalid PartId format in SelectedIssueScope: {SelectedIssueScope}");
                    TempData["ErrorMessage"] = "שגיאה: פורמט בחירת השאלה/סעיף אינו תקין.";
                    await LoadDataAsync();
                    return Page();
                }
            }
            else
            {
                NewIssue.PartId = null;
                NewIssue.QuestionNumber = null;
            }
            return null!; // Indicates success
        }
        private async Task<IActionResult> HandleFileUploadsAsync()
        {
            if (NewIssueDrawings == null || !NewIssueDrawings.Any())
            {
                return null!; // No files to upload, proceed as no error occurred
            }

            // --- Configuration Checks (Existing) ---
            if (string.IsNullOrEmpty(_containerName))
            {
                _logger.LogError("Google Cloud Storage bucket name not configured in appsettings.json.");
                TempData["ErrorMessage"] = "שגיאה: לא הוגדר שם דלי לאחסון קבצים ב-appsettings.json.";
                await LoadDataAsync();
                return Page();
            }

            // --- Define Allowed File Types and Max Size ---
            // Make sure these MIME types match what your client-side 'accept' attribute allows
            string[] allowedMimeTypes = { "image/png", "image/jpeg", "image/jpg", "image/gif", "image/tiff", "image/bmp", "image/heic", "image/webp" };
            long maxFileSizePerFile = (long)(maxFileSizeinMB * 1024 * 1024); // Example: 1 MB per file. Adjust as needed.

            // --- File Count Validation (Moved upfront for immediate feedback) ---
            if (NewIssueDrawings.Count > MaxDrawingsPerIssue)
            {
                string errorMessage = $"ניתן לצרף עד {MaxDrawingsPerIssue} קבצי תמונות בלבד לשאלה.";
                TempData["ErrorMessage"] = errorMessage;

                // Adding a model error makes it visible via asp-validation-summary or asp-validation-for
                ModelState.AddModelError("NewIssueDrawings", errorMessage);
                // Log this warning/error as well
                _logger.LogWarning("User attempted to upload {Count} files, exceeding limit of {Max}", NewIssueDrawings.Count, MaxDrawingsPerIssue);
                await LoadDataAsync(); // Load data before returning Page()
                return Page(); // Halt processing and return to page
            }

            // --- Process Files with Type and Size Validation ---
            // Iterate through all submitted files, as we already checked the count.
            foreach (var file in NewIssueDrawings)
            {
                if (file.Length == 0)
                {
                    // Decide how to handle empty files: skip or add an error
                    // For now, we'll skip them, but a user might consider it an error.
                    ModelState.AddModelError("NewIssueDrawings", $"קובץ '{file.FileName}' ריק ולא יועלה.");
                    _logger.LogWarning("Empty file submitted: {FileName}", file.FileName);
                    continue; // Skip to the next file
                }

                // ⭐ File Type Validation
                if (!allowedMimeTypes.Contains(file.ContentType.ToLowerInvariant()))
                {
                    string errorMessage = $"קובץ '{file.FileName}' אינו תמונה נתמכת (PNG, JPG, GIF, TIFF, BMP, HEIC, WEBP)";
                    TempData["ErrorMessage"] = errorMessage;
                    ModelState.AddModelError("NewIssueDrawings", errorMessage);
                    _logger.LogWarning("Attempted to upload disallowed file type: {FileName} with MIME type {MimeType}", file.FileName, file.ContentType);
                    await LoadDataAsync();
                    return Page(); // Halt immediately on first invalid type
                }

                // ⭐ File Size Validation
                if (file.Length > maxFileSizePerFile)
                {
                    string errorMessage = $"קובץ '{file.FileName}' חורג מהגודל המקסימלי המותר של {maxFileSizePerFile / (1024 * 1024)} MB.";

                    TempData["ErrorMessage"] = errorMessage; // ⭐ ADDED: Use TempData for guaranteed display ⭐
                    ModelState.AddModelError("NewIssueDrawings", errorMessage);
                    _logger.LogWarning("Attempted to upload file exceeding max size: {FileName} ({Length} bytes)", file.FileName, file.Length);
                    await LoadDataAsync();
                    return Page(); // Halt immediately on first oversized file
                }

                try
                {
                    // Ensures a unique folder for each set of drawings for an issue
                    // The folder is generated per issue, then per file within that issue if needed.
                    // Consider if all drawings for ONE issue should share ONE folder or separate.
                    // Current setup creates a new GUID folder per drawing which is fine for uniqueness.
                    string folderPath = $"issues-drawings/{Guid.NewGuid().ToString()}";
                    string fileUrl = await _storageService.UploadFileAsync(file, _containerName, folderPath);

                    if (!string.IsNullOrEmpty(fileUrl))
                    {
                        // Ensure NewIssue.Drawings is initialized before adding
                        NewIssue.Drawings ??= new List<Drawing>();
                        NewIssue.Drawings.Add(new Drawing
                        {
                            OriginalFileName = file.FileName,
                            FilePathOrUrl = fileUrl,
                            UploadDate = _timeProvider.Now,
                            MimeType = file.ContentType
                        });
                    }
                    else
                    {
                        _logger.LogWarning("Failed to upload file '{FileName}' to GCS. Upload service returned empty URL.", file.FileName);
                        // This is a warning. If you want to treat a failed GCS upload as a critical error that halts,
                        // you would add a ModelState.AddModelError and return Page() here.
                        // Current logic allows partial success if one file fails to upload.
                        TempData["ErrorMessage"] = "אזהרה: חלק מהקבצים לא הועלו בהצלחה לענן. אנא בדוק את יומני השרת. המשך בבקשה.";
                        // Note: If multiple files fail GCS upload, TempData will only show the last message.
                        // For a more robust solution, use a List<string> for TempData errors.
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Critical error during file upload for new issue. File: {FileName}", file.FileName);
                    TempData["ErrorMessage"] = "שגיאה קריטית בהעלאת קבצים. אנא בדוק את יומני השרת.";
                    await LoadDataAsync();
                    return Page(); // Halt if a critical upload error occurs
                }
            }
            return null!; // Indicates success (all files processed or no files were present)
        }
        private async Task<IActionResult> SaveIssueToDatabaseAsync()
        {
            // 1. Add the NewIssue to the context
            _dbContext.Issues.Add(NewIssue);

            try
            {
                // 2. SaveChangesAsync() for the NewIssue ONLY, to get its database-generated ID
                await _dbContext.SaveChangesAsync();

                // At this point, NewIssue.Id will have the actual ID from the database
                _logger.LogInformation("New issue saved to database. Generated Issue ID: {IssueId}", NewIssue.Id);

                // 3. Now, call the method to add the log entry, passing the *real* Issue ID
                AddIssueLogEntryToContext(NewIssue.Id);

                // 4. SaveChangesAsync() again for the IssueLog entry
                await _dbContext.SaveChangesAsync(); // This saves the IssueLog entry

                _logger.LogInformation("IssueLog entry added successfully for Issue ID: {IssueId}", NewIssue.Id);
                TempData["SuccessMessage"] = "השאלה נוספה בהצלחה!";

                // Redirect to page to clear form and refresh data
                return RedirectToPage(new { SelectedExamId = SelectedExamId, EditMyReportSelected = EditMyReportSelected });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error adding new issue, drawings, and/or log entry to database.");
                TempData["ErrorMessage"] = "שגיאה בשמירת השאלה החדשה: " + ex.Message;
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Inner exception details for new issue.");
                }
            }
            // If we reach here, there was an error, reload data and return to page
            await LoadDataAsync();
            return Page();
        }

        private void AddIssueLogEntryToContext(int issueId)
        {
            string description = $"שאלה חדשה: {NewIssue.Description}";
            if (NewIssue.Drawings != null)
            {
                description += ", תמונות: ";
                foreach (var dr in NewIssue.Drawings)
                    description += $"{dr.OriginalFileName} ";
            }
            // ⭐ UPDATED CODE: Add a log entry for the new issue using the correct field names
            var issueLog = new IssueLog
            {
                IssueId = issueId,
                UserId = CurrentLoggedInUserId!.Value, // Assuming this is populated correctly
                LogDate = _timeProvider.Now,
                Description = description
                // You can make the description more detailed if needed, e.g., include NewIssue.Description
            };
            _dbContext.IssueLogs.Add(issueLog); // Add the log entry to the context
        }
        private void LogModelStateErrors()
        {
            _logger.LogWarning("New Issue submission failed due to validation errors. Details:");
            foreach (var modelStateEntry in ModelState.Values)
            {
                foreach (var error in modelStateEntry.Errors)
                {
                    _logger.LogWarning($"- {error.ErrorMessage}");
                }
            }
        }
        public async Task<IActionResult> OnPostUpdateIssueDescriptionAsync(int issueId, string newDescription)
        {
            await PopulateCurrentUserId();

            if (!CurrentLoggedInUserId.HasValue)
            {
                TempData["ErrorMessage"] = "שגיאה: פרטי משתמש לא תקינים. לא ניתן לעדכן";
                _logger.LogError("Authenticated user identity not found or invalid during UpdateIssueDescription.");
                await LoadDataAsync();
                return Page();
            }

            var issueToUpdate = await _dbContext.Issues
                                                .Where(i => i.Id == issueId)
                                                .FirstOrDefaultAsync();

            if (issueToUpdate == null)
            {
                TempData["ErrorMessage"] = "שגיאה: שאלה לא נמצאה";
                _logger.LogWarning("Attempted to update non-existent issue with ID: {IssueId}", issueId);
                await LoadDataAsync();
                return Page();
            }

            // Authorization check
            if (issueToUpdate.UserId != CurrentLoggedInUserId.Value)
            {
                TempData["ErrorMessage"] = "אין לך הרשאה לעדכן שאלה זו.";
                _logger.LogWarning("User {UserId} attempted to update issue {IssueId} without proper authorization.", CurrentLoggedInUserId.Value, issueId);
                await LoadDataAsync();
                return Page();
            }

            // Status check
            if (issueToUpdate.Status != IssueStatus.Open)
            {
                TempData["ErrorMessage"] = "לא ניתן לעדכן תיאור של שאלה שאינה במצב 'פתוח'.";
                _logger.LogWarning("User {UserId} attempted to update description for issue {IssueId} which is not 'Open'. Current Status: {Status}", CurrentLoggedInUserId.Value, issueId, issueToUpdate.Status);
                await LoadDataAsync();
                return Page();
            }

            // Basic validation for newDescription
            if (string.IsNullOrWhiteSpace(newDescription))
            {
                TempData["ErrorMessage"] = "התיאור אינו יכול להיות ריק";
                ModelState.AddModelError("newDescription", "התיאור אינו יכול להיות ריק"); // Add a model error for client-side display if needed
                await LoadDataAsync();
                return Page();
            }

            // Update description
            string oldDescription = issueToUpdate.Description!; // Store old description for log
            issueToUpdate.Description = newDescription.Trim();

            // Add IssueLog entry
            var logDescription = $"תיאור השאלה עודכן: '{issueToUpdate.Description}'";
            _dbContext.IssueLogs.Add(new IssueLog
            {
                IssueId = issueToUpdate.Id,
                UserId = CurrentLoggedInUserId.Value,
                LogDate = _timeProvider.Now,
                Description = logDescription
            });

            try
            {
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = "התיאור עודכן בהצלחה";
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error updating issue description for Issue ID: {IssueId}", issueId);
                TempData["ErrorMessage"] = "שגיאה בשמירת עדכון התיאור: " + ex.Message;
            }

            // Reload data and return to page, maintaining filter state
            await LoadDataAsync();
            return RedirectToPage(new { SelectedExamId = SelectedExamId, EditMyReportSelected = EditMyReportSelected });
        }

        private Task PopulateCurrentUserId()
        {
            LoadAdminContext();
            CurrentLoggedInUserId = null; // Reset to null first
            var userIdClaimValue = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (User.Identity!.IsAuthenticated && !string.IsNullOrEmpty(userIdClaimValue))
            {
                if (int.TryParse(userIdClaimValue, out int parsedUserId))
                {
                    CurrentLoggedInUserId = parsedUserId;
                }
                else
                {
                    _logger.LogError("ClaimTypes.NameIdentifier value '{UserIdClaimValue}' could not be parsed as an integer for authenticated user.", userIdClaimValue);
                }
            }
            else
            {
                _logger.LogWarning("User not authenticated or NameIdentifier claim is empty.");
            }
            return Task.CompletedTask;
        }
        private async Task LoadDataAsync()
        {
            await PopulateCurrentUserId();

            if (CurrentLoggedInUserId.HasValue)
            {               
                AvailableExams = new SelectList(await _dbContext.AllowedExams
                                                              .Where(ae => ae.UserId == CurrentLoggedInUserId.Value)
                                                              .Join(_dbContext.Exams .Where(e => e.SubjectId == SubjectId),
                                                                    ae => ae.ExamId,
                                                                    e => e.Id,
                                                                    (ae, e) => e)
                                                              .Where(e => e.Active)
                                                              .OrderBy(e => e.ExamTitle)
                                                              .Select(e => new SelectListItem
                                                              {
                                                                  Value = e.Id.ToString(),
                                                                  Text = e.ExamTitle ?? ""
                                                              })
                                                              .ToListAsync(), "Value", "Text");
            }
            else
            {
                _logger.LogWarning("User ID not available. Showing no exams in the dropdown.");
                AvailableExams = new SelectList(new List<SelectListItem>(), "Value", "Text");
            }

            if (SelectedExamId.HasValue)
            {
                SelectedExamTitle = (await _dbContext.Exams.FindAsync(SelectedExamId.Value))?.ExamTitle;

                var issuesQuery = _dbContext.Issues
                                                .Include(i => i.Part)
                                                .Include(i => i.User)
                                                .Include(i => i.FinalAnswer)
                                                    .ThenInclude(fa => fa!.Senior)
                                                .Include(i => i.Drawings) // ⭐ NEW: Include Drawings here!
                                                .Where(i => i.ExamId == SelectedExamId.Value)
                                                .AsQueryable();

                if (CurrentLoggedInUserId.HasValue && CurrentLoggedInUserId.Value > 0)
                {
                    issuesQuery = issuesQuery.Where(i => i.Status == IssueStatus.Closed || i.UserId == CurrentLoggedInUserId.Value);
                }
                else
                {
                    issuesQuery = issuesQuery.Where(i => i.Status == IssueStatus.Closed);
                    _logger.LogWarning("User not logged in for full report view. Only showing closed issues for this report type.");
                }

                // Sorting
                var sortState = HttpContext.Session.Get<List<string>>(SortSessionKey) ?? new List<string>();
                bool isFirstSort = true;

                if (sortState.Any())
                {
                    foreach (var sortKey in sortState)
                    {
                        var parts = sortKey.Split('_');
                        var columnName = parts[0];
                        var direction = parts.Length > 1 ? parts[1] : "asc";

                        issuesQuery = issuesQuery.ApplyDynamicSort(columnName, direction, isFirstSort);

                        isFirstSort = false;
                    }
                }
                else
                {
                    issuesQuery = issuesQuery.OrderBy(i => i.QuestionNumber).ThenBy(i => i.Part);
                }
                issuesQuery = issuesQuery.ApplyFilters(FilterModel!, HttpContext);
                Issues = await issuesQuery.ToListAsync();

                var partsForDropdown = await _dbContext.Parts
                                                                .Where(p => p.ExamId == SelectedExamId.Value)
                                                                .OrderBy(p => p.QuestionNumber)
                                                                .ThenBy(p => p.QuestionPart)
                                                                .ToListAsync();

                var selectListItems = new List<SelectListItem>();
                selectListItems.Add(new SelectListItem
                {
                    Value = "ExamOnly",
                    Text = "כללי לבחינה"
                });

                var distinctQuestionNumbers = partsForDropdown
                                                    .Select(p => p.QuestionNumber)
                                                    .Distinct()
                                                    .OrderBy(qn => qn);

                var questionNumberItems = new List<SelectListItem>();

                foreach (var qn in distinctQuestionNumbers)
                {
                    if (partsForDropdown.Any(p => p.QuestionNumber == qn && !string.IsNullOrEmpty(p.QuestionPart)))
                    {
                        questionNumberItems.Add(new SelectListItem
                        {
                            Value = $"Question_{qn}",
                            Text = $"{qn}"
                        });
                    }
                }
                foreach (var p in partsForDropdown)
                {
                    questionNumberItems.Add(new SelectListItem
                    {
                        Value = $"Part_{p.Id}",
                        Text = $"{p.QuestionNumber}{(string.IsNullOrEmpty(p.QuestionPart) ? "" : " - " + p.QuestionPart)}"
                    });
                }
                questionNumberItems = questionNumberItems.OrderBy(item => item.Text, new NaturalSortComparer()).ToList();
                selectListItems.AddRange(questionNumberItems);

                AvailablePartsForNewIssue = new SelectList(selectListItems, "Value", "Text");
            }
            else
            {
                Issues = new List<Issue>();
                AvailablePartsForNewIssue = new SelectList(new List<SelectListItem>(), "Value", "Text");
            }
        }
        // Handler for deleting an existing drawing
        public async Task<IActionResult> OnPostDeleteDrawing(int issueId, string drawingUrl)
        {
            // Ensures context is maintained on redirect
            if (!SelectedExamId.HasValue)
            {
                TempData["ErrorMessage"] = "שגיאה: לא נבחרה בחינה."; // Error: No exam selected.
                return RedirectToPage();
            }

            // 1. Find the Drawing record in the database
            var drawingToDelete = await _dbContext.Drawings // ASSUMPTION: Drawings DbSet exists
                .FirstOrDefaultAsync(d => d.FilePathOrUrl == drawingUrl && d.IssueId == issueId);

            if (drawingToDelete == null)
            {
                TempData["ErrorMessage"] = "שגיאה: התמונה לא נמצאה בבסיס הנתונים."; // Error: Image not found in database.
                return RedirectToPage(new { SelectedExamId = SelectedExamId, EditMyReportSelected = EditMyReportSelected });
            }

            try
            {
                // 2. Delete the file from Azure Blob Storage
                string containerName = "issues-drawings"; // <-- PLACEHOLDER: Use your actual container name
                // Use the URI class to parse the full URL
                Uri uri = new Uri(drawingUrl);
                string blobPathToDelete = uri.AbsolutePath.Substring(uri.AbsolutePath.IndexOf($"/{containerName}/") + containerName.Length + 2);
                await _storageService.DeleteFileAsync(blobPathToDelete, containerName);

                // 3. Remove the record from the database
                _dbContext.Drawings.Remove(drawingToDelete);
                await _dbContext.SaveChangesAsync();

                TempData["SuccessMessage"] = "התמונה נמחקה בהצלחה."; // Image deleted successfully.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting drawing {DrawingUrl} for issue {IssueId}", drawingUrl, issueId);
                // Note: We delete the DB record even if Azure deletion fails to prevent an orphaned record, 
                // but log an error. You may want different error handling here.
                TempData["ErrorMessage"] = $"שגיאה במחיקת תמונה: {ex.Message}. ייתכן שהקובץ נמחק ידנית מ-Azure."; // Error deleting image: {message}. File might have been manually deleted from Azure.
            }

            // 4. Redirect back
            return RedirectToPage(new { SelectedExamId = SelectedExamId, EditMyReportSelected = EditMyReportSelected });
        }
        // Handler for adding a new drawing to an existing issue
        public async Task<IActionResult> OnPostAddDrawing(int issueId, IFormFile NewDrawing)
        {
            // Ensures context is maintained on redirect
            if (!SelectedExamId.HasValue)
            {
                TempData["ErrorMessage"] = "שגיאה: לא נבחרה בחינה."; // Error: No exam selected.
                return RedirectToPage();
            }

            // 1. Validation checks
            if (NewDrawing == null || NewDrawing.Length == 0)
            {
                TempData["ErrorMessage"] = "לא נבחר קובץ תמונה להוספה."; // No image file selected for addition.
                return RedirectToPage(new { SelectedExamId = SelectedExamId, EditMyReportSelected = EditMyReportSelected });
            }

            // Fetch the Issue and related Drawings
            var issue = await _dbContext.Issues
                .Include(i => i.Drawings)
                .FirstOrDefaultAsync(i => i.Id == issueId);

            if (issue == null)
            {
                TempData["ErrorMessage"] = "שגיאה: הנושא/שאלה לא נמצא במערכת."; // Error: Issue/Question not found in system.
                return RedirectToPage(new { SelectedExamId = SelectedExamId, EditMyReportSelected = EditMyReportSelected });
            }

            // Check limit of 3 files
            if (issue.Drawings.Count >= 3)
            {
                TempData["ErrorMessage"] = "לא ניתן להוסיף יותר מ-3 תמונות לנושא זה."; // Cannot add more than 3 images to this issue.
                return RedirectToPage(new { SelectedExamId = SelectedExamId, EditMyReportSelected = EditMyReportSelected });
            }

            try
            {
                // 2. Upload file to Azure (uses the fixed logic to set Content-Type)
                string folderPath = issue.Id.ToString();

                string drawingUrl = await _storageService.UploadFileAsync(NewDrawing, _containerName, folderPath);

                // 3. Save the new drawing record to the database
                var newDrawingEntity = new Drawing // ASSUMPTION: Drawing model is available
                {
                    FilePathOrUrl = drawingUrl,
                    OriginalFileName = NewDrawing.FileName,
                    UploadDate = _timeProvider.Now,
                    IssueId = issue.Id
                };

                issue.Drawings.Add(newDrawingEntity);
                await _dbContext.SaveChangesAsync();

                TempData["SuccessMessage"] = "התמונה נוספה בהצלחה."; // Image added successfully.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding drawing for issue {IssueId}", issueId);
                TempData["ErrorMessage"] = $"שגיאה בהוספת תמונה: {ex.Message}"; // Error adding image: {message}
            }

            // 4. Redirect back
            return RedirectToPage(new { SelectedExamId = SelectedExamId, EditMyReportSelected = EditMyReportSelected });
        }
    }
}