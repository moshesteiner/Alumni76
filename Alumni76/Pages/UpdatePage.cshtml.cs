using Alumni76.Data;
using Alumni76.Models;
using Alumni76.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq; // <--- ADD THIS LINE
using System.Security.Claims;
using System.Threading.Tasks;

namespace Alumni76.Pages
{
    [Authorize]
    public class UpdatePageModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<UpdatePageModel> _logger;
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEmailService _emailService;

        private const string specialAdminId = "special_admin_user_id";

        [TempData]
        public string? SuccessMessage { get; set; }
        [TempData]
        public string? ErrorMessage { get; set; }

        public int? UserId { get; set; }

        public User? UserToDisplay { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "שם פרטי נדרש")]
        [StringLength(100, ErrorMessage = "שם פרטי לא יכול לחרוג מ-100 תווים")]
        public string? FirstName { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "שם משפחה נדרש")]
        [StringLength(100, ErrorMessage = "שם משפחה לא יכול לחרוג מ-100 תווים")]
        public string? LastName { get; set; }

        [BindProperty]
        [StringLength(100, ErrorMessage = "שם נעורים לא יכול לחרוג מ-100 תווים")]
        public string? MaidenName { get; set; }

        [BindProperty]
        [StringLength(100, ErrorMessage = "כינוי לא יכול לחרוג מ-100 תווים")]
        public string? NickName { get; set; }

        [BindProperty]
        [StringLength(8)]
        public string? Class { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "כתובת אימייל נדרשת")]
        [EmailAddress(ErrorMessage = "פורמט אימייל לא תקין")]
        [StringLength(255, ErrorMessage = "כתובת אימייל לא יכולה לחרוג מ-255 תווים")]
        public string? Email { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "טלפון נדרש")]
        [RegularExpression(@"^(\+?\d{1,3}[- ]?)?\(?(\d{3})\)?[- ]?(\d{3,4})[- ]?(\d{4})$|^0?(\d{2,3})[\s-]?(\d{3})[\s-]?(\d{4})$", ErrorMessage = "מספר לא תקין")]
        [StringLength(15, ErrorMessage = "מספר הטלפון ארוך מדי")] // Max length allowing formatting characters (hyphens/spaces)
        public string? Phone1 { get; set; }

        [BindProperty]
        [RegularExpression(@"^(\+?\d{1,3}[- ]?)?\(?(\d{3})\)?[- ]?(\d{3,4})[- ]?(\d{4})$|^0?(\d{2,3})[\s-]?(\d{3})[\s-]?(\d{4})$", ErrorMessage = "מספר לא תקין")]
        [StringLength(15, ErrorMessage = "מספר הטלפון ארוך מדי")] // Max length allowing formatting characters (hyphens/spaces)
        public string? Phone2 { get; set; }

        [BindProperty]
        [StringLength(200, ErrorMessage = "כתובת לא יכול לחרוג מ-200 תווים")]
        public string? Address { get; set; }

        [BindProperty]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "הסיסמה חייבת להיות באורך של לפחות {2} תווים ובאורך מקסימלי של {1} תווים", MinimumLength = 6)]
        public string? NewPassword { get; set; }

        [BindProperty]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "הסיסמה ואישור הסיסמה אינם תואמים")]
        public string? ConfirmPassword { get; set; }
        public UpdatePageModel(ILogger<UpdatePageModel> logger, ApplicationDbContext dbContext, IEmailService emailService,
                        IPasswordHasher<User> passwordHasher)
        {
            _logger = logger;
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
            _emailService = emailService!;
        }

        private bool TryGetCurrentUserIdAndType(out int? numericUserId, out bool isSpecialAdmin)
        {
            numericUserId = null;
            isSpecialAdmin = false;

            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null)
                {
                    if (string.Equals(userIdClaim.Value, specialAdminId, StringComparison.OrdinalIgnoreCase))
                    {
                        isSpecialAdmin = true;
                        _logger.LogInformation("Special admin user '{Email}' accessed UpdatePage.", User.Identity.Name);
                        return true;
                    }
                    else if (int.TryParse(userIdClaim.Value, out int parsedUserId))
                    {
                        numericUserId = parsedUserId;
                        UserId = parsedUserId;
                        return true;
                    }
                }
                _logger.LogError("Authenticated user's NameIdentifier claim not found or not an integer/special ID. Claim value: '{ClaimValue}'", userIdClaim?.Value);
            }
            _logger.LogWarning("Attempted to access UpdatePage without authenticated user or with invalid ID.");
            return false;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            ModelState.Clear();
            if (!TryGetCurrentUserIdAndType(out int? userId, out bool isSpecialAdmin))
            {
                ErrorMessage = "נדרש להתחבר כדי לעדכן פרטים.";
                return RedirectToPage("/Index");
            }

            if (isSpecialAdmin)
            {
                ErrorMessage = "דף זה אינו רלוונטי עבור משתמש אדמין מיוחד. אין לך פרופיל לעדכן במערכת.";
                return Page();
            }

            UserToDisplay = await _dbContext.Users.FindAsync(userId!.Value);

            if (UserToDisplay == null)
            {
                ErrorMessage = "המשתמש המחובר לא נמצא במערכת.";
                _logger.LogError($"Logged-in user with ID {userId} not found in database.");
                return RedirectToPage("/Index");
            }

            UpdateFromUserToDisplay(UserToDisplay);

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            TempData.Remove("NeedsEmailVerification");
            if (!TryGetCurrentUserIdAndType(out int? userId, out bool isSpecialAdmin))
            {
                ErrorMessage = "שגיאת הרשאה: לא ניתן לעדכן פרטים ללא משתמש מחובר.";
                return RedirectToPage("/Index");
            }

            if (isSpecialAdmin)
            {
                ErrorMessage = "משתמש אדמין מיוחד לא יכול לעדכן פרטים בדף זה.";
                return Page();
            }

            // Retrieve the user from the database to get current values and track changes
            UserToDisplay = await _dbContext.Users.FindAsync(userId!.Value);
            if (UserToDisplay == null)
            {
                ErrorMessage = "המשתמש המחובר לא נמצא במערכת, לא ניתן לעדכן.";
                _logger.LogWarning($"Attempted to update non-existent user (ID from claims: {userId}).");
                return RedirectToPage("/Index");
            }

            var originalEmailFromDb = UserToDisplay.Email;

            // Validate properties based on data annotations (FirstName, LastName, Email format, Password length)
            if (!ModelState.IsValid)
            {
                ErrorMessage = "ישנן שגיאות בטופס. נא לתקן אותן.";
                // Re-populate UserToDisplay's properties with the current form values for redisplay
                RecoverUserToDisplay(UserToDisplay);
                return Page();
            }

            // Only check for email duplication if the email has actually changed
            var emailChaned = !string.Equals(Email, originalEmailFromDb, StringComparison.OrdinalIgnoreCase);
            if (emailChaned)
            {
                var isEmailTaken = await _dbContext.Users.AnyAsync(u => u.Email == Email && u.Id != UserToDisplay.Id);
                if (isEmailTaken)
                {
                    ModelState.AddModelError("Email", "כתובת אימייל זו כבר נמצאת בשימוש.");
                    ErrorMessage = "ישנן שגיאות בטופס. נא לתקן אותן.";
                    // Re-populate UserToDisplay's properties with the current form values for redisplay
                    RecoverUserToDisplay(UserToDisplay);

                    return Page(); // Return immediately to show the specific email error
                }

                // Verify Email change
                await VerifyEmail(UserToDisplay);
            }

            // Normalize Phone Format Before Processing             
            Phone1 = FormatPhoneNumber(Phone1);
            Phone2 = FormatPhoneNumber(Phone2);

            // Apply updated values from the form to the tracked entity
            RecoverUserToDisplay(UserToDisplay);

            var passwordChanged = !string.IsNullOrWhiteSpace(NewPassword);
            if (passwordChanged)
            {
                UserToDisplay.PasswordHash = _passwordHasher.HashPassword(UserToDisplay, NewPassword!);
                _logger.LogInformation($"Password for User {userId} updated (hashed).");
            }

            try
            {
                await _dbContext.SaveChangesAsync();
                SuccessMessage = $"הפרטים עודכנו בהצלחה עבור '{UserToDisplay.FirstName} {UserToDisplay.LastName}'.";
                _logger.LogInformation($"User {userId} updated: FirstName='{FirstName}', LastName='{LastName}', Email='{Email}'.");

                // Get the InitiatorId from the current user's claims
                var initiatorIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                int? initiatorId = null; // Initialize as nullable int

                // Attempt to parse the string ID to an integer
                if (initiatorIdString != null && int.TryParse(initiatorIdString, out int parsedId))
                {
                    initiatorId = parsedId;
                }

                _logger.LogInformation($"UserLog entry created for profile update of user {UserToDisplay.Id} by initiator {initiatorId}.");

                // Refresh claims if name or email changed (email isn't directly in standard Name claim, but good practice to refresh)
                var originalClaimsPrincipal = HttpContext.User;
                if (originalClaimsPrincipal.Identity?.IsAuthenticated == true)
                {
                    var claims = originalClaimsPrincipal.Claims
                                         .Where(c => c.Type != ClaimTypes.Name) // Remove old Name claim
                                         .ToList();

                    // Add updated Name claim
                    claims.Add(new Claim(ClaimTypes.Name, $"{UserToDisplay.FirstName} {UserToDisplay.LastName}"));
                    // If you also store email in claims, update that here too:
                    // claims.Add(new Claim(ClaimTypes.Email, UserToDisplay.Email));

                    var newClaimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var newClaimsPrincipal = new ClaimsPrincipal(newClaimsIdentity);

                    await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, newClaimsPrincipal);

                    _logger.LogInformation($"User claims for '{UserToDisplay.Email}' refreshed with new name: '{UserToDisplay.FirstName} {UserToDisplay.LastName}'.");
                }
            }
            catch (DbUpdateException ex)
            {
                // This catch block will now handle other DbUpdateExceptions (e.g., database connection issues,
                // other unique constraints you might have not explicitly checked).
                // Email duplication is now handled pre-emptively.
                ErrorMessage = $"שגיאה בזמן עדכון הפרטים: {ex.Message}";
                _logger.LogError(ex, $"Failed to update user {userId}.");
            }

            // If Email Changed, verify by sending a code to the new Email
            if (TempData["NeedsEmailVerification"] != null)
            {
                TempData["UserIdForVerification"] = UserToDisplay.Id;
                return RedirectToPage("/VerifyCode");
            }
            return RedirectToPage();
        }
        private async Task VerifyEmail(User user)
        {
            user.EmailVerified = false;    // Mark as unverified
            user.PendingEmail = Email;

            //  Generate a new code (same logic as FirstTimeUser)
            var code = new Random().Next(100000, 999999).ToString();
            user.TwoFactorCode = code;
            user.TwoFactorCodeExpiration = DateTime.UtcNow.AddMinutes(15);

            // Send the email notification to the NEW email address
            var emailSubject = "אימות כתובת אימייל חדשה";
            var emailBody = $"<div style='direction:rtl;'>שלום {user.FirstName},<br>שינית את כתובת האימייל שלך. קוד האימות החדש הוא: <b>{code}</b></div>";

            await _emailService.SendEmailAsync(Email!, emailSubject, emailBody);

            // Will redirect them to VerifyCode AFTER the SaveChangesAsync()
            TempData["NeedsEmailVerification"] = true;
        }
        private void RecoverUserToDisplay(User user)
        {          
            user.FirstName = FirstName!;
            user.LastName = LastName!;
            user.MaidenName = MaidenName;
            user.NickName = NickName;
            user.Class = Class;
            user.Phone1 = Phone1!;
            user.Phone2 = Phone2!;
            user.Address = Address;

            // ONLY update email if it hasn't changed. 
            // If it HAS changed, the OnPostAsync handles it via PendingEmail.
            if (user.Email.Equals(Email, StringComparison.OrdinalIgnoreCase))
            {
                user.Email = Email!;
            }
        }
        private void UpdateFromUserToDisplay(User user)
        {
            FirstName = user.FirstName;
            LastName = user.LastName;
            MaidenName = user.MaidenName;
            NickName = user.NickName;
            Class = user.Class;
            Email = user.Email;
            Phone1 = user.Phone1;
            Phone2 = user.Phone2;
            Address = user.Address;
        }
        private string FormatPhoneNumber(string? phone)
        {
            if (string.IsNullOrWhiteSpace(phone)) return string.Empty;

            // 1. Remove all non-digits to analyze the pattern
            string cleaned = new string(phone.Where(char.IsDigit).ToArray());

            // 2. Handle International Israel code (972...) -> Convert to local (0...)
            if (cleaned.StartsWith("972") && cleaned.Length > 10)
            {
                cleaned = "0" + cleaned.Substring(3);
            }

            // 3. Israeli Mobile (10 digits: 05X-XXX-XXXX)
            if (cleaned.Length == 10 && cleaned.StartsWith("05"))
            {
                return $"{cleaned.Substring(0, 3)}-{cleaned.Substring(3, 3)}-{cleaned.Substring(6)}";
            }

            // 4. Israeli Landline (9 digits: 0X-XXX-XXXX)
            if (cleaned.Length == 9 && cleaned.StartsWith("0"))
            {
                return $"{cleaned.Substring(0, 2)}-{cleaned.Substring(2, 3)}-{cleaned.Substring(5)}";
            }

            // 5. US/Canada Format (11 digits starting with 1: 1-XXX-XXX-XXXX)
            if (cleaned.Length == 11 && cleaned.StartsWith("1"))
            {
                return $"{cleaned.Substring(0, 1)}-{cleaned.Substring(1, 3)}-{cleaned.Substring(4, 3)}-{cleaned.Substring(7)}";
            }

            // 6. If it's some other international length, return the cleaned digits 
            // (at least this removes the mess if they typed weirdly)
            return cleaned.Length > 0 ? cleaned : phone;
        }
    }
}