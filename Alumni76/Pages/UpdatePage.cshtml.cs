using Alumni76.Data;
using Alumni76.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using System.Linq; // <--- ADD THIS LINE

namespace Alumni76.Pages
{
    [Authorize(Roles = "Evaluator, Senior, Admin")]
    public class UpdatePageModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly ILogger<UpdatePageModel> _logger;
        private readonly IPasswordHasher<User> _passwordHasher;

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
        [Required(ErrorMessage = "כתובת אימייל נדרשת")]
        [EmailAddress(ErrorMessage = "פורמט אימייל לא תקין")]
        [StringLength(255, ErrorMessage = "כתובת אימייל לא יכולה לחרוג מ-255 תווים")]
        public string? Email { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "טלפון נדרש")]
        [RegularExpression(@"^0?(\d{2,3})[\s-]?(\d{7})$", ErrorMessage = "פורמט טלפון לא חוקי. (דוגמה: 05X-XXXXXXX)")]
        [StringLength(15, ErrorMessage = "מספר הטלפון ארוך מדי")] // Max length allowing formatting characters (hyphens/spaces)
        public string? Phone1 { get; set; }

        [BindProperty]
        [DataType(DataType.Password)]
        [StringLength(100, ErrorMessage = "הסיסמה חייבת להיות באורך של לפחות {2} תווים ובאורך מקסימלי של {1} תווים", MinimumLength = 6)]
        public string? NewPassword { get; set; }

        [BindProperty]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "הסיסמה ואישור הסיסמה אינם תואמים")]
        public string? ConfirmPassword { get; set; }
        public UpdatePageModel(ILogger<UpdatePageModel> logger, ApplicationDbContext dbContext,
                        IPasswordHasher<User> passwordHasher)
        {
            _logger = logger;
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
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

            FirstName = UserToDisplay.FirstName;
            LastName = UserToDisplay.LastName;
            Email = UserToDisplay.Email;
            Phone1 = UserToDisplay.Phone1;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
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

            // Store original values before assigning new ones from BindProperty
            var originalFirstName = UserToDisplay.FirstName;
            var originalLastName = UserToDisplay.LastName;
            var originalEmailFromDb = UserToDisplay.Email; 
            var originalPhone = UserToDisplay.Phone1;

            // Validate properties based on data annotations (FirstName, LastName, Email format, Password length)
            if (!ModelState.IsValid)
            {
                ErrorMessage = "ישנן שגיאות בטופס. נא לתקן אותן.";
                // Re-populate UserToDisplay's properties with the current form values for redisplay
                UserToDisplay.FirstName = FirstName;
                UserToDisplay.LastName = LastName;
                UserToDisplay.Email = Email;
                UserToDisplay.Phone1 = Phone1!;
                return Page();
            }

            // --- START EMAIL DUPLICATION CHECK ---
            // Only check for email duplication if the email has actually changed
            if (!string.Equals(Email, originalEmailFromDb, StringComparison.OrdinalIgnoreCase))
            {
                var isEmailTaken = await _dbContext.Users.AnyAsync(u => u.Email == Email && u.Id != UserToDisplay.Id);
                if (isEmailTaken)
                {
                    ModelState.AddModelError("Email", "כתובת אימייל זו כבר נמצאת בשימוש.");
                    ErrorMessage = "ישנן שגיאות בטופס. נא לתקן אותן.";
                    // Re-populate UserToDisplay's properties with the current form values for redisplay
                    UserToDisplay.FirstName = FirstName;
                    UserToDisplay.LastName = LastName;
                    UserToDisplay.Email = Email;
                    UserToDisplay.Phone1 = Phone1!;

                    return Page(); // Return immediately to show the specific email error
                }
            }
            // --- END EMAIL DUPLICATION CHECK ---
            // Normalize Phone Format Before Processing 
            string cleanPhone = new string(Phone1!.Where(char.IsDigit).ToArray());
            if (cleanPhone.Length == 10) // Standard 05X-XXXXXXX, 07X-XXXXXXX (10 digits)
            {
                Phone1 = $"{cleanPhone.Substring(0, 3)}-{cleanPhone.Substring(3)}";
            }
            else if (cleanPhone.Length == 9) // Standard 0X-XXXXXXX (9 digits)
            {
                Phone1 = $"{cleanPhone.Substring(0, 2)}-{cleanPhone.Substring(2)}";
            }

            // Apply updated values from the form to the tracked entity
            UserToDisplay.FirstName = FirstName;
            UserToDisplay.LastName = LastName;
            UserToDisplay.Email = Email; // Assign the new, validated email
            UserToDisplay.Phone1 = Phone1!;

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

            return RedirectToPage();
        }
    }
}