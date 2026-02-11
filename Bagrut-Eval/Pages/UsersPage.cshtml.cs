using Bagrut_Eval.Data;
using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Bagrut_Eval.Pages.Common;
using Bagrut_Eval.Utilities;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System; // For Guid and DateTime.UtcNow
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims; // Required for User.FindFirst(ClaimTypes.NameIdentifier)
using System.Text.Json;
using System.Threading.Tasks;

[Authorize(Roles = "Admin")]
public class UsersPageModel : BasePageModel<UsersPageModel>
{
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IEmailService _emailService;

    [BindProperty(SupportsGet = true)]
    public FilterModel? FilterModel { get; set; } //= new FilterModel();
    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Dir { get; set; }

    //public IList<User>? Users { get; set; }
    //public IList<UserSubject>? UsersAdmin { get; set; }
    public List<UserDisplayModel>? DisplayUsers { get; set; }
    public List<SelectListItem>? AvailableRoles { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }
    [TempData]
    public string? ErrorMessage { get; set; }

    public UsersPageModel(ApplicationDbContext dbContext, ILogger<UsersPageModel> logger, IPasswordHasher<User> passwordHasher,
                         IEmailService emailService, ITimeProvider timeProvider) : base(dbContext, logger, timeProvider)
    {
        _passwordHasher = passwordHasher;
        _emailService = emailService;
    }
    public class ExamViewModel
    {
        public int Id { get; set; }
        public string? ExamTitle { get; set; }
    }

    // Model for the partial view to manage assignments
    public class ManageSubjectsViewModel
    {
        public int UserId { get; set; }
        public List<SubjectAssignmentItem> SubjectAssignments { get; set; } = new List<SubjectAssignmentItem>();
        public List<string> AvailableRoles { get; set; } = new List<string> { "Evaluator", "Senior", "Admin" };
    }
    // Model for a single subject assignment within the modal (includes assignment status)
    public class SubjectAssignmentItem
    {
        public int SubjectId { get; set; }
        public string? Title { get; set; }
        public bool IsAssigned { get; set; }
        public string? CurrentRole { get; set; }
    }
    public class AssignedSubjectModel
    {
        public int SubjectId { get; set; }
        public string? Title { get; set; }
        public string? Role { get; set; }
        public bool Active { get; set; }
    }
    public class UpdateSubjectsData
    {
        public int UserId { get; set; }
        public List<int>? SelectedSubjectIds { get; set; } // List of checked Subject IDs
    }
    public class UpdateUserSubjectsRequest
    {
        public int UserId { get; set; }
        public List<SubjectRoleAssignment> Assignments { get; set; } = new List<SubjectRoleAssignment>();
    }

    public class SubjectRoleAssignment
    {
        public int SubjectId { get; set; }
        public string Role { get; set; } = string.Empty; // Role must not be null due to database schema
        public bool IsAssigned { get; set; } // Used to determine Add/Remove/Update
    }
    public class UserDisplayModel
    {
        public int UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Role { get; set; }

        // User-Specific Data
        public bool Active { get; set; }
        public bool SubjectActive { get; set; }
        public ICollection<AllowedExam> AllowedExams { get; set; } = new List<AllowedExam>();
        public ICollection<LastLogin> LoginRecords { get; set; } = new List<LastLogin>();
        public List<AssignedSubjectModel> AssignedSubjects { get; set; } = new List<AssignedSubjectModel>();
        public int PrimarySubjectId => AssignedSubjects.FirstOrDefault()?.SubjectId ?? 0;
    }

    public new async Task OnGetAsync()
    {
        ModelState.Clear();
        await base.OnGetAsync();
        CheckForSpecialAdmin();

        string? json = HttpContext.Session.GetString(FilterSessionKey);
        if (json != null)
        {
            FilterModel = JsonSerializer.Deserialize<FilterModel>(json) ?? FilterModel;
        }
        else
        {
            SetFilterModel();
        }
        var sortState = new List<string>();
        if (!string.IsNullOrEmpty(Sort))
        {
                sortState.Add($"{Sort}_{(Dir == "desc" ? "desc" : "asc")}");
        }
        ViewData["SortState"] = sortState;
        await LoadUsersAndSelectListsAsync();
    }
    private void SetFilterModel()
    {
        if (FilterModel == null)
            FilterModel = new FilterModel();
        FilterModel.DisplayUserNameSearch = true;
        FilterModel.DisplayShowActiveOrOpen = true;
        FilterModel.DisplayDescriptionSearch = false;
        FilterModel.DisplayFilterDate = false;
        FilterModel.DisplayShowClosed = false;
        FilterModel.DisplayShowNewerThanLastLogin = false;
        FilterModel.DisplaySubjectSearch = IsSpecialAdmin ? true : false;
        string json = JsonSerializer.Serialize(FilterModel);
        HttpContext.Session.SetString(FilterSessionKey, json);
    }
    private IQueryable<User> ApplySort(IQueryable<User> query)
    {
        if (string.IsNullOrEmpty(Sort))
        {
            // Default sort order if no sort is specified
            return query = query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName);
        }
        else
        {
            switch (Sort)
            {
                case "FirstName":
                    return (Dir == "desc") ?
                        query.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName) :
                        query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName);
                case "LastName":
                    return (Dir == "desc") ?
                        query.OrderByDescending(u => u.LastName).ThenByDescending(u => u.FirstName) :
                        query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName);
                case "LastLogin":
                    return (Dir == "desc") ?
                        query.OrderByDescending(u => u.LoginRecords.Max(lr => (DateTime?)lr.LoginDate)) :
                        query.OrderBy(u => u.LoginRecords.Max(lr => (DateTime?)lr.LoginDate));
                case "Role":
                    if (Dir == "desc")
                    {
                        return query.OrderByDescending(u => u.UserSubjects
                                         .Where(us => us.SubjectId == SubjectId)
                                         .Select(us => us.Role)
                                         .FirstOrDefault() ?? "zzzzz" // Ensure 'Unknown' or users without a role sort last (desc)
                                    );
                    }
                    else
                    {
                        return query.OrderBy(u => u.UserSubjects
                                         .Where(us => us.SubjectId == SubjectId)
                                         .Select(us => us.Role)
                                         .FirstOrDefault() ?? "zzzzz" // Ensure 'Unknown' or users without a role sort last (asc)
                                      );
                    }
                default:
                    return query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName);
            }
        }
    }
    private async Task LoadUsersAndSelectListsAsync()
    {
        CheckForSpecialAdmin();
        int currentSubjectId = 0;
        if (!IsSpecialAdmin)
        {
            var subjectIdClaim = User.Claims.FirstOrDefault(c => c.Type == SubjectIdClaimType);

            if (int.TryParse(subjectIdClaim?.Value, out currentSubjectId))
            {
                SubjectId = currentSubjectId;
            }
            else
            {
                SubjectId = null;
                // You might want to log an error here, as a Regular Admin should always have this claim.
            }

            SubjectTitle = User.FindFirstValue(SubjectTitleClaimType) ?? string.Empty;
        }
        else
        {
            SubjectId = null;
            SubjectTitle = string.Empty;
        }
        DisplayUsers = new List<UserDisplayModel>();

        if (!IsSpecialAdmin && currentSubjectId > 0)
        {
            IQueryable<User> query = _dbContext.Users;
            query = query.Where(u => u.UserSubjects!
                            .Any(us => us.SubjectId == currentSubjectId))
                            .Include(u => u.UserSubjects!.Where(us => us.SubjectId == currentSubjectId))
                                 .ThenInclude(us => us.Subject)
                            .Include(u => u.AllowedExams)
                                .ThenInclude(ae => ae.Exam)
                            .Include(u => u.LoginRecords)
                            .OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
                            .AsNoTracking();

            query = query.ApplyFilters(FilterModel!);
            query = ApplySort(query);
            var users = await query.ToListAsync();

            DisplayUsers = users.Select(u =>
            {
                var currentAssignment = u.UserSubjects.FirstOrDefault();
                return new UserDisplayModel
                {
                    UserId = u.Id,
                    FirstName = u.FirstName,
                    LastName = u.LastName,
                    Email = u.Email,
                    Phone = u.Phone,
                    Active = u.Active,
                    SubjectActive = currentAssignment?.Active ?? false,
                    AllowedExams = u.AllowedExams,
                    LoginRecords = u.LoginRecords,
                    Role = currentAssignment?.Role ?? "Unknown",
                    AssignedSubjects = u.UserSubjects.Select(us => new AssignedSubjectModel
                    {
                        SubjectId = us.SubjectId,
                        Title = us.Subject!.Title,
                        Role = us.Role,
                        Active = us.Active
                    }).ToList()
                };
            }).ToList();
        }
        else if (IsSpecialAdmin)
        {
            IQueryable<User> queryU = _dbContext.Users;
            queryU = queryU.Include(u => u.LoginRecords)
                 .Include(u => u.AllowedExams)
                     .ThenInclude(ue => ue.Exam)
                 .Include(u => u.UserSubjects) // Include the join table
                     .ThenInclude(us => us.Subject) // Then include the actual Subject entity
                 .OrderBy(u => u.FirstName)
                 .ThenBy(u => u.LastName)
                 .AsNoTracking();

            queryU = queryU.ApplyFilters(FilterModel!);
            queryU = ApplySort(queryU);
            var assignments = await queryU.ToListAsync();
            DisplayUsers = assignments.Select(u => new UserDisplayModel
            {
                UserId = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                Email = u.Email,
                Phone = u.Phone,
                Active = u.Active,
                AllowedExams = u.AllowedExams, // User's exams list
                LoginRecords = u.LoginRecords,
                AssignedSubjects = u.UserSubjects.Select(us => new AssignedSubjectModel
                {
                    SubjectId = us.SubjectId,
                    Title = us.Subject!.Title,
                    Role = us.Role,
                    Active = us.Active
                }).ToList()
            }).ToList();
        }

        AvailableRoles = new List<SelectListItem>
        {
            new SelectListItem { Value = "Evaluator", Text = "Evaluator" },
            new SelectListItem { Value = "Senior", Text = "Senior" },
            new SelectListItem { Value = "Admin", Text = "Administrator" }
        };
    }

    // Handler for updating a user's role and/or status
    public async Task<IActionResult> OnPostUpdateUserAsync(int userId, int subjectToUpdateId, string newRole, bool newStatus)
    {
        LoadAdminContext();
        if (userId == CurrentUserId)
        {
            return RedirectToPage();   // Don't allow the Admin user to modify his own data
        }
        string? json = HttpContext.Session.GetString(FilterSessionKey);
        if (json != null)
            FilterModel = JsonSerializer.Deserialize<FilterModel>(json) ?? FilterModel;
        if (string.IsNullOrWhiteSpace(newRole) && !IsSpecialAdmin)
        {
            ErrorMessage = "Role and status cannot be empty.";
            await LoadUsersAndSelectListsAsync();
            return Page();
        }
        int effectiveSubjectId = subjectToUpdateId;
        if (!IsSpecialAdmin && effectiveSubjectId == 0 && SubjectId > 0)
        {
            effectiveSubjectId = SubjectId.Value;
        }
        if (effectiveSubjectId == 0) // Now check the final, effective ID
        {
            ErrorMessage = "שגיאה: חסר הקשר מקצוע לעדכון תפקיד";
            await LoadUsersAndSelectListsAsync();
            return Page();
        }

        var userToUpdate = await _dbContext.Users
                .Include(u => u.UserSubjects!.Where(us => us.SubjectId == effectiveSubjectId))
                .FirstOrDefaultAsync(u => u.Id == userId);

        if (userToUpdate == null)
        {
            ErrorMessage = "מעריך לא נמצא";
            _logger.LogWarning($"Attempted to update non-existent user with ID: {userId}");
            await LoadUsersAndSelectListsAsync();
            return Page();
        }

        var assignmentToUpdate = userToUpdate.UserSubjects!.FirstOrDefault();
        if (assignmentToUpdate == null)
        {
            ErrorMessage = $"שגיאה: המעריך {userToUpdate.FirstName} אינו משויך למקצוע הנוכחי (ID: {SubjectId}).";
            _logger.LogError($"Admin attempted to update role for user {userId} who is not assigned to subject {SubjectId}.");
            await LoadUsersAndSelectListsAsync();
            return Page();
        }
        assignmentToUpdate.Role = newRole;
        assignmentToUpdate.Active = newStatus;

        try
        {
            await _dbContext.SaveChangesAsync();

            // Get the InitiatorId (the ID of the currently logged-in Admin)
            var initiatorIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? initiatorId = null; // Initialize as nullable int

            // Attempt to parse the string ID to an integer
            if (initiatorIdString != null && int.TryParse(initiatorIdString, out int parsedId))
            {
                initiatorId = parsedId;
            }

            // Corrected: Parse the string "True"/"False" to boolean for correct status display
            var activeStatusText = userToUpdate.Active ? "פעיל" : "לא פעיל";
            string userRoleForLog = assignmentToUpdate.Role;
            var descriptionForRoleStatusChange = $"שינוי פרטים: {userToUpdate.FirstName} {userToUpdate.LastName}, שינוי תפקיד: {userRoleForLog} (במקצוע ID: {SubjectId}), סטטוס: {activeStatusText}";
            var userLog = new UserLog
            {
                UserId = userToUpdate.Id,
                InitiatorId = initiatorId,
                Date = DateTime.UtcNow,
                Description = descriptionForRoleStatusChange
            };
            _dbContext.UsersLog.Add(userLog);
            await _dbContext.SaveChangesAsync(); // Save the log entry

            SuccessMessage = $"מעריך '{userToUpdate.FirstName} {userToUpdate.LastName}' עודכן בהצלחה!";
            _logger.LogInformation($"User {userId} updated by an admin. New Role: {newRole}, New Status: {newStatus}.");
        }
        catch (DbUpdateException ex)
        {
            ErrorMessage = $"שגיאה בזמן עדכון המעריך: {ex.Message}";
            _logger.LogError(ex, $"Failed to update user {userId}.");
        }

        return RedirectToPage();
    }

    //SpecialUpdateUser
    public async Task<IActionResult> OnPostSpecialUpdateUserAsync(int userId, int subjectToUpdateId, string newRole, bool newStatus)
    {
        LoadAdminContext();
        string? json = HttpContext.Session.GetString(FilterSessionKey);
        if (json != null)
            FilterModel = JsonSerializer.Deserialize<FilterModel>(json) ?? FilterModel;
        if (string.IsNullOrWhiteSpace(newRole) && !IsSpecialAdmin)
        {
            ErrorMessage = "Role and status cannot be empty, and must be modified by SpecialAdmin";
            await LoadUsersAndSelectListsAsync();
            return Page();
        }

        var userToUpdate = await _dbContext.Users
                //.Include(u => u.UserSubjects!.Where(us => us.SubjectId == effectiveSubjectId))
                .FirstOrDefaultAsync(u => u.Id == userId);

        if (userToUpdate == null)
        {
            ErrorMessage = "מעריך לא נמצא";
            _logger.LogWarning($"Attempted to update non-existent user with ID: {userId}");
            await LoadUsersAndSelectListsAsync();
            return Page();
        }

        userToUpdate.Active = newStatus;

        try
        {
            await _dbContext.SaveChangesAsync();

            // Get the InitiatorId (the ID of the currently logged-in Admin)
            var initiatorIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? initiatorId = null; // Initialize as nullable int

            // Attempt to parse the string ID to an integer
            if (initiatorIdString != null && int.TryParse(initiatorIdString, out int parsedId))
            {
                initiatorId = parsedId;
            }

            // Corrected: Parse the string "True"/"False" to boolean for correct status display
            var activeStatusText = userToUpdate.Active ? "פעיל" : "לא פעיל";
            var descriptionForRoleStatusChange = $"שינוי פרטים: {userToUpdate.FirstName} {userToUpdate.LastName},  סטטוס: {activeStatusText}";
            var userLog = new UserLog
            {
                UserId = userToUpdate.Id,
                InitiatorId = initiatorId,
                Date = DateTime.UtcNow,
                Description = descriptionForRoleStatusChange
            };
            _dbContext.UsersLog.Add(userLog);
            await _dbContext.SaveChangesAsync(); // Save the log entry

            SuccessMessage = $"מעריך '{userToUpdate.FirstName} {userToUpdate.LastName}' עודכן בהצלחה!";
            _logger.LogInformation($"User {userId} updated by an admin. New Role: {newRole}, New Status: {newStatus}.");
        }
        catch (DbUpdateException ex)
        {
            ErrorMessage = $"שגיאה בזמן עדכון סטטוס המעריך: {ex.Message}";
            _logger.LogError(ex, $"Failed to update user {userId}.");
        }

        return RedirectToPage();
    }

    // Handler for password reset
    public async Task<IActionResult> OnPostResetPasswordAsync(int userId)
    {
        var userToReset = await _dbContext.Users.FindAsync(userId);

        if (userToReset == null)
        {
            ErrorMessage = "מעריך לא נמצא לצורך איפוס סיסמה.";
            _logger.LogWarning($"Attempted to reset password for non-existent user with ID: {userId}");
            return RedirectToPage();
        }

        string newTemporaryPassword = "TempP@ss" + Guid.NewGuid().ToString().Substring(0, 5);
        userToReset.PasswordHash = _passwordHasher.HashPassword(userToReset, newTemporaryPassword);

        try
        {
            await _dbContext.SaveChangesAsync();
            SuccessMessage = $"הסיסמה עבור '{userToReset.FirstName} {userToReset.LastName}' אופסה בהצלחה. הסיסמה הזמנית החדשה היא: <strong>{newTemporaryPassword}</strong>. אנא ודא שהמשתמש משנה אותה מיד!";
            // Send email notification to the user
            await _emailService.ResetPasswordEmailAsync(userToReset.Email!, userToReset.FirstName!, newTemporaryPassword);
            _logger.LogInformation($"Password reset for user {userId} by admin. Temporary password: {newTemporaryPassword}");
        }
        catch (DbUpdateException ex)
        {
            ErrorMessage = $"שגיאה בזמן איפוס הסיסma: {ex.Message}";
            _logger.LogError(ex, $"Failed to reset password for user {userId}.");
        }

        return RedirectToPage();
    }

    // --- HANDLER FOR GETTING EXAMS FOR MODAL ---
    public async Task<JsonResult> OnGetExamsForUserAsync(int userId/*, int subjectId*/)
    {
        LoadAdminContext();   // populate SubjectId and SubjectTitle
        var user = await _dbContext.Users
                                    .Include(u => u.AllowedExams) // Make sure AllowedExams are loaded
                                    .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return new JsonResult(new { success = false, message = "User not found." }) { StatusCode = 404 };
        }

        var availableExams = await _dbContext.Exams
                                            .Where(e => e.Active) // Only active exams
                                            .Where(e => e.SubjectId == SubjectId)
                                            .OrderBy(e => e.ExamTitle)
                                            .Select(e => new ExamViewModel { Id = e.Id, ExamTitle = e.ExamTitle, })
                                            .ToListAsync();

        var allowedExamIds = user.AllowedExams.Select(ae => ae.ExamId).ToList();

        return new JsonResult(new { success = true, availableExams = availableExams, allowedExamIds = allowedExamIds });
    }

    // --- HANDLER FOR UPDATING ALLOWED EXAMS ---
    public async Task<IActionResult> OnPostUpdateAllowedExamsAsync([FromBody] UpdateAllowedExamsRequest request)
    {
        if (request == null)
        {
            return new JsonResult(new { success = false, message = "Invalid request data." }) { StatusCode = 400 };
        }

        var user = await _dbContext.Users
                                    .Include(u => u.AllowedExams)
                                    .FirstOrDefaultAsync(u => u.Id == request.UserId);

        if (user == null)
        {
            return new JsonResult(new { success = false, message = "User not found." }) { StatusCode = 404 };
        }

        // 1. Get ALL exams (by ID) that belong to the subject being updated.
        var examIdsInCurrentSubject = await _dbContext.Exams
                                                      .Where(e => e.SubjectId == request.SubjectId)
                                                      .Select(e => e.Id)
                                                      .ToListAsync();

        // 2. Identify exams to REMOVE:
        //    a. Must be currently allowed (user.AllowedExams)
        //    b. Must belong to the current subject (examIdsInCurrentSubject.Contains)
        //    c. Must NOT be in the newly selected list (!request.SelectedExamIds.Contains)
        var examsToRemove = user.AllowedExams
                                .Where(ae => examIdsInCurrentSubject.Contains(ae.ExamId))
                                .Where(ae => !request.SelectedExamIds.Contains(ae.ExamId))
                                .ToList();

        _dbContext.AllowedExams.RemoveRange(examsToRemove);


        // 3. Identify exams to ADD (This logic remains correct, as it only adds new ones)
        var existingAllowedExamIds = user.AllowedExams.Select(ae => ae.ExamId).ToList();
        var examsToAdd = request.SelectedExamIds
                                .Where(examId => !existingAllowedExamIds.Contains(examId))
                                .Select(examId => new AllowedExam { UserId = user.Id, ExamId = examId })
                                .ToList();

        _dbContext.AllowedExams.AddRange(examsToAdd);

        try
        {
            await _dbContext.SaveChangesAsync();

            // Get the InitiatorId (the ID of the currently logged-in Admin)
            var initiatorIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? initiatorId = null; // Initialize as nullable int

            // Attempt to parse the string ID to an integer
            if (initiatorIdString != null && int.TryParse(initiatorIdString, out int parsedId))
            {
                initiatorId = parsedId;
            }

            // Re-fetch the *actual* titles of the exams the user is now allowed to take
            var currentAllowedExamTitles = await _dbContext.Exams
                                                        .Where(e => request.SelectedExamIds.Contains(e.Id))
                                                        .Select(e => e.ExamTitle)
                                                        .ToListAsync();

            var descriptionForAllowedListChange = $"שינוי ברשימת הבחינות: {user.FirstName} {user.LastName}, {user.Email}, Status: {(user.Active ? "פעיל" : "לא פעיל")}, בחינות: {string.Join(", ", currentAllowedExamTitles)}";

            var userLog = new UserLog
            {
                UserId = user.Id,
                InitiatorId = initiatorId,
                Date = DateTime.UtcNow,
                Description = descriptionForAllowedListChange
            };
            _dbContext.UsersLog.Add(userLog);
            await _dbContext.SaveChangesAsync(); // Save the log entry
            // --- END ADDITION ---

            return new JsonResult(new { success = true, message = "Allowed exams updated successfully." });
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, $"Error updating allowed exams for user {request.UserId}.");
            return new JsonResult(new { success = false, message = "שגיאה בעדכון בחינות מורשות." }) { StatusCode = 500 };
        }
    }

    // DTO for incoming AJAX request to update allowed exams
    public class UpdateAllowedExamsRequest
    {
        public int UserId { get; set; }
        public int SubjectId { get; set; }
        public List<int> SelectedExamIds { get; set; } = new List<int>();
    }
    public IActionResult OnPostFilter()
    {
        return RedirectToPage(new { Sort = Sort, Dir = Dir });
    }
    public IActionResult OnPostApplyFilter()
    {
        CheckForSpecialAdmin();
        if (FilterModel != null)
            FilterModel.DisplaySubjectSearch = IsSpecialAdmin;
        string json = JsonSerializer.Serialize(FilterModel);
        HttpContext.Session.SetString(FilterSessionKey, json);

        // Redirect back to GET
        return RedirectToPage(new
        {
            Sort = Sort,
            Dir = Dir
        });
    }


    // AJAX GET handler to fetch data and render the partial view for the Subjects modal.
    public async Task<IActionResult> OnGetManageSubjectsPartial(int userId)
    {
        // Ensure you call this to authorize access before proceeding
        CheckForSpecialAdmin();
        if (!IsSpecialAdmin)
        {
            // This will now return 403, which is the correct security response
            return Forbid();
        }

        // 1. Get ALL subjects and the user's current assignments
        var allSubjects = await _dbContext.Subjects
            .Where(s => s.Id > 1)   // skip default subject
            .OrderBy(s => s.Title)
            .ToListAsync();

        var currentAssignments = await _dbContext.UserSubjects
            .Where(us => us.UserId == userId)
            .ToDictionaryAsync(us => us.SubjectId);

        // 2. Build the ViewModel
        var model = new ManageSubjectsViewModel
        {
            UserId = userId,
            SubjectAssignments = allSubjects.Select(s =>
            {
                currentAssignments.TryGetValue(s.Id, out UserSubject? assignment);

                return new SubjectAssignmentItem
                {
                    SubjectId = s.Id,
                    Title = s.Title,
                    IsAssigned = assignment != null,
                    CurrentRole = assignment?.Role // Assign the current role, or null/empty if not assigned
                };
            }).ToList()
        };

        // 3. Return the partial view
        return Partial("_ManageSubjectsPartial", model);
    }

    // AJAX POST handler to save the changes made in the Subjects modal.
    public async Task<IActionResult> OnPostUpdateUserSubjects([FromBody] UpdateUserSubjectsRequest request)
    {
        // 1. Authorization/Validation (ensure CheckForSpecialAdmin() is called here if needed)
        CheckForSpecialAdmin();
        if (!IsSpecialAdmin) { return Forbid(); }

        if (request.Assignments == null)
        {
            return new JsonResult(new { success = false, message = "אין נתונים לעדכון." });
        }

        var user = await _dbContext.Users
            .Include(u => u.UserSubjects)
            .FirstOrDefaultAsync(u => u.Id == request.UserId);

        if (user == null) { return new JsonResult(new { success = false, message = "משתמש לא נמצא." }); }

        var currentAssignments = user.UserSubjects.ToDictionary(us => us.SubjectId);
        var assignmentsToProcess = request.Assignments.ToList();

        var assignmentsToAdd = new List<UserSubject>();
        var assignmentsToRemove = new List<UserSubject>();

        foreach (var assignmentData in assignmentsToProcess)
        {
            var subjectId = assignmentData.SubjectId;
            var newRole = assignmentData.Role;
            var isAssigned = assignmentData.IsAssigned;

            if (currentAssignments.TryGetValue(subjectId, out UserSubject? existingAssignment))
            {
                // Case A: Assignment EXISTS
                if (isAssigned)
                {
                    // Update Role if subject is assigned and the role is different
                    if (existingAssignment.Role != newRole)
                    {
                        existingAssignment.Role = newRole;
                        _dbContext.UserSubjects.Update(existingAssignment);
                    }
                }
                else
                {
                    // Remove Assignment if it exists but is now unchecked
                    assignmentsToRemove.Add(existingAssignment);
                }
            }
            else if (isAssigned)
            {
                // Case B: Assignment is NEW (Add)
                assignmentsToAdd.Add(new UserSubject
                {
                    UserId = request.UserId,
                    SubjectId = subjectId,
                    Role = newRole // Role is explicitly set here
                });
            }
        }

        // Apply changes
        _dbContext.UserSubjects.RemoveRange(assignmentsToRemove);
        _dbContext.UserSubjects.AddRange(assignmentsToAdd);

        await _dbContext.SaveChangesAsync();

        return new JsonResult(new { success = true, message = "מקצועות ותפקידים עודכנו בהצלחה." });
    }
}