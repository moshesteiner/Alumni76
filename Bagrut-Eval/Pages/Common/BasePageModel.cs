using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Bagrut_Eval.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Bagrut_Eval.Pages.Common
{
    //
    // Use this class to allow common functionality accross all pages.
    // Here the hbrew translation or Roles is given as an example, and not needed any more
    //
    // In apge that uses this class don't forget to
    //     1. inherit from BasePageModel and
    //     2. add a using Bagrut_Eval.Pages.Common;
    //     3. call base.OnGetAsync() or base.OnPostAsync() in the page's OnGetAsync or OnPostAsync methods
    //    See Index.vshtml.cs for an example.
    //
    public abstract class BasePageModel<T> : PageModel
    {
        protected readonly ApplicationDbContext _dbContext;
        protected readonly ILogger<T> _logger;
        protected readonly ITimeProvider _timeProvider;

        [TempData]
        public string? LoggedInUserName { get; set; }

        [TempData]
        public string? LoggedInUserRole { get; set; }
        [ViewData]
        public List<User> LoggedInSeniors { get; set; } = new List<User>();

        [BindProperty(SupportsGet = true)]
        public int? SubjectId { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? SubjectTitle { get; set; }

        protected int CurrentUserId = 0;
        protected const string specialAdminId = "special_admin_user_id";       

        [BindProperty(SupportsGet = true)]
        public bool IsSpecialAdmin { get; set; } = false;
        public List<Exam>? AvailableExamsList { get; set; } //= new List<Exam>();
        public SelectList? AvailableExams { get; set; }

        [BindProperty]
        public List<int> SelectedExamIds { get; set; } = new List<int>();

        public string FilterSessionKey = "UsersFilterState";
        public string SubjectIdClaimType = "SubjectId";
        public string SubjectTitleClaimType = "SubjectTitle";

        public BasePageModel(ApplicationDbContext dbContext, ILogger<T> logger, ITimeProvider timeProvider)
        {
            _dbContext = dbContext;
            _logger = logger;
            _timeProvider = timeProvider;
        }
        public Dictionary<string, string> RoleDisplayNames { get; set; } = new Dictionary<string, string>
        {
            { "Admin", "מנהל מערכת" },
            { "Senior", "מעריך בכיר" },
            { "Evaluator", "מעריך" }
        };

        public string DisplayRole { get; set; } = string.Empty;

        protected bool IsValidRole(string? role)
        {
            if (string.IsNullOrWhiteSpace(role))
            {
                return false;
            }
            return RoleDisplayNames.ContainsKey(role.Trim());
        }
        public string GetIssueStatusDisplay(IssueStatus status)
        {
            return status switch
            {
                IssueStatus.Open => "פתוח",
                IssueStatus.Closed => "סגור",
                IssueStatus.InProgress => "בתהליך",
                IssueStatus.Resolved => "נפתר",
                _ => "לא ידוע"
            };
        }
        public string GetIssueStatusTag(IssueStatus status)
        {
            return status switch
            {
                IssueStatus.Open => "<span class='badge bg-warning text-dark'>פתוח</span>", // Open
                IssueStatus.Closed => "<span class='badge bg-success'>סגור</span>", // Closed
                IssueStatus.InProgress => "<span class='badge bg-info text-dark'>בטיפול</span>", // In Progress
                IssueStatus.Resolved => "<span class='badge bg-primary'>נפתר</span>", // Resolved
                _ => "<span class='badge bg-secondary'>לא ידוע</span>" // Unknown/Fallback
            };
        }
        protected void CheckForSpecialAdmin()
        {
            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            IsSpecialAdmin = string.Equals(userIdClaim?.Value, specialAdminId, StringComparison.OrdinalIgnoreCase);
            if (userIdClaim != null)
            {
                int.TryParse( userIdClaim.Value, out CurrentUserId);
            }
        }
        protected void LoadAdminContext()
        {
            CheckForSpecialAdmin(); // Assume this sets IsSpecialAdmin

            if (IsSpecialAdmin)
            {
                SubjectId = null;
                SubjectTitle = string.Empty;
            }
            else
            {
                // Logic to load SubjectId and SubjectTitle from claims
                var subjectIdClaim = User.Claims.FirstOrDefault(c => c.Type == SubjectIdClaimType);

                if (int.TryParse(subjectIdClaim?.Value, out int currentSubjectId))
                {
                    SubjectId = currentSubjectId;
                }
                else
                {
                    SubjectId = null;
                }

                SubjectTitle = User.FindFirstValue(SubjectTitleClaimType) ?? string.Empty;
            }
        }

        protected virtual Task OnGetAsync()
        {
            LoadAdminContext();
            SetDisplayRole();
            if (IsSpecialAdmin)
            {
                ViewData["special"] = "true";
            }
            return Task.CompletedTask; // This is fine if BasePageModel doesn't have other async ops
        }

        protected virtual Task OnPostAsync()
        {
            LoadAdminContext();
            SetDisplayRole();
            return Task.CompletedTask; // This is fine if BasePageModel doesn't have other async ops
        }

        protected void SetDisplayRole()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var loggedInUserRole = User.FindFirst(ClaimTypes.Role)?.Value;
                if (!string.IsNullOrEmpty(loggedInUserRole) && RoleDisplayNames.ContainsKey(loggedInUserRole))
                {
                    DisplayRole = RoleDisplayNames[loggedInUserRole];
                }
                else
                {
                    DisplayRole = loggedInUserRole!; // Fallback to English if not found
                }
            }
            else
            {
                DisplayRole = string.Empty; // Clear role if not authenticated
            }
        }
        // --- RECORD LOGIN AND LIMIT ENTRIES ---
        protected async Task RecordLoginAndLimit(int userId)
        {
            // 1. Record the new login
            var newLogin = new LastLogin
            {
                UserId = userId,
                LoginDate = DateTime.UtcNow // Use UTC for consistency
            };
            _dbContext.LastLogins.Add(newLogin);

            // 2. Get existing logins for this user, ordered by date descending
            var existingLogins = await _dbContext.LastLogins
                .Where(ll => ll.UserId == userId)
                .OrderByDescending(ll => ll.LoginDate)
                .ToListAsync();

            // 3. If there are more than 10 logins, remove the oldest ones
            if (existingLogins.Count >= 10) // Use >= in case some older ones were left from previous runs
            {
                var loginsToRemove = existingLogins.Skip(10).ToList(); // Skip the 10 most recent
                _dbContext.LastLogins.RemoveRange(loginsToRemove);
            }

            // 4. Save changes to the database
            await _dbContext.SaveChangesAsync();
            _logger.LogInformation("Recorded login for user {UserId} and limited entries to 10.", userId);
        }
        public override async Task<IActionResult> OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
        {
            // 1. Execute the page's handler (OnGet, OnPost)
            var executedContext = await next(); // Renamed to 'executedContext' for clarity

            // 2. --- LOGIC TO LOAD SENIOR USERS WHO ARE LOGGED-IN TO DISPLAY IN FOOTER ---
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int currentUserId))
                {
                    var activeUserIds = LoggedInUsers.GetActiveUserIds();

                    LoggedInSeniors = await _dbContext.Users
                        .Where(u => activeUserIds.Contains(u.Id) &&
                                    u.Id != currentUserId) // Filter by active users and not the current user first
                        .Join(_dbContext.UserSubjects, // Perform the join
                            user => user.Id,
                            userSubject => userSubject.UserId,
                            (user, userSubject) => new { User = user, UserSubject = userSubject })
                        .Where(j => (j.UserSubject.Role == "Senior" || j.UserSubject.Role == "Admin") &&
                             j.UserSubject.SubjectId == SubjectId) 
                        .Select(j => j.User) // Select only the User object
                        .Distinct() // Ensure we only get one User object per user (in case they have multiple subjects)
                        .OrderBy(u => u.FirstName) .ThenBy(u => u.LastName)
                        .ToListAsync();
                }
            }

            return executedContext.Result!;
        }
        // Helper function to be used in any page model's OnGetAsync or business logic
        public (int? Id, string? Title) GetUserSubject()
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return (null, null);
            }

            var subjectIdClaim = User.FindFirst(SubjectIdClaimType);
            var subjectTitleClaim = User.FindFirst(SubjectTitleClaimType);

            if (subjectIdClaim != null && int.TryParse(subjectIdClaim.Value, out int id) && subjectTitleClaim != null)
            {
                HttpContext.Session.SetString("Subject", subjectTitleClaim.Value);
                return (id, subjectTitleClaim.Value);
            }

            return (null, null);
        }
    }
}