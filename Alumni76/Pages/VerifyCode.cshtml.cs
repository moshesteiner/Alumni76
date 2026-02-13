using Alumni76.Data;
using Alumni76.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Alumni76.Pages.Common;

namespace Alumni76.Pages
{
    public class VerifyCodeModel : BasePageModel<VerifyCodeModel>
    {
        [BindProperty]
        public int UserId { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "קוד חובה")]
        [Display(Name = "קוד אימות")]
        public string? Code { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "סיסמה חדשה חובה")]
        [DataType(DataType.Password)]
        [Display(Name = "סיסמה חדשה")]
        public string? NewPassword { get; set; }

        [BindProperty]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "הסיסמאות אינן תואמות")]
        [Display(Name = "אימות סיסמה חדשה")]
        public string? ConfirmPassword { get; set; }

        private readonly IPasswordHasher<User> _passwordHasher;

        public User? CurrentUser { get; set; }

        public VerifyCodeModel(ApplicationDbContext context, IPasswordHasher<User> passwordHasher,
                               ILogger<VerifyCodeModel> logger, ITimeProvider timeProvider) : base(context, logger, timeProvider)
        {
            _passwordHasher = passwordHasher;
        }

        public new async Task<IActionResult> OnGetAsync()
        {
            await base.OnGetAsync();
            // Get the UserId from TempData. If it doesn't exist, redirect them back to login.
            if (TempData["UserIdForVerification"] is int userId)
            {
                UserId = userId;
                CurrentUser = await _dbContext.Users.FindAsync(UserId);
                if (CurrentUser == null) return RedirectToPage("/Index");
                TempData.Keep("UserIdForVerification");
                return Page();
            }
            return RedirectToPage("/Index");
        }



        public new async Task<IActionResult> OnPostAsync()
        {
            await base.OnPostAsync();
            CurrentUser = await _dbContext.Users.FindAsync(UserId);

            if (CurrentUser != null && CurrentUser.LastLogin != null)
            {
                ModelState.Remove("NewPassword");
                ModelState.Remove("ConfirmPassword");
            }

            if (!ModelState.IsValid) return Page();

            if (CurrentUser == null || CurrentUser.TwoFactorCode != Code || CurrentUser.TwoFactorCodeExpiration < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, "קוד האימות שגוי או פג תוקף.");
                return Page();
            }

            bool isFirstTimeLogin = CurrentUser.LastLogin == null;

            // Handle Email Swap
            if (!string.IsNullOrEmpty(CurrentUser.PendingEmail))
            {
                CurrentUser.Email = CurrentUser.PendingEmail;
                CurrentUser.PendingEmail = null;
                CurrentUser.EmailVerified = true;
            }

            // Update Status/Password
            if (isFirstTimeLogin)
            {
                CurrentUser.LastLogin = DateTime.UtcNow;
                CurrentUser.EmailVerified = true;
            }

            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                CurrentUser.PasswordHash = _passwordHasher.HashPassword(CurrentUser, NewPassword!);
            }

            CurrentUser.TwoFactorCode = null;
            CurrentUser.TwoFactorCodeExpiration = null;

            await _dbContext.SaveChangesAsync();

            // --- CONDITIONAL AUTH LOGIC ---

            if (isFirstTimeLogin)
            {
                // For first-timers, stick to your original plan: Clear everything and make them log in fresh.
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                TempData["SuccessMessage"] = "הגדרת החשבון הושלמה! כעת ניתן להתחבר עם הסיסמה החדשה.";
                return RedirectToPage("/Index");
            }
            else
            {
                // For existing users changing email: Refresh claims so they stay logged in.
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, $"{CurrentUser.FirstName} {CurrentUser.LastName}"),
                    new Claim(ClaimTypes.NameIdentifier, CurrentUser.Id.ToString()),
                    new Claim(ClaimTypes.Role, CurrentUser.IsAdmin ? "Admin" : "Member"),
                    new Claim(ClaimTypes.Email, CurrentUser.Email)
                };

                var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                TempData["SuccessMessage"] = "כתובת האימייל אומתה ועודכנה בהצלחה.";
                return RedirectToPage("/UpdatePage");
            }
        }


        public new async Task<IActionResult> _OnPostAsync()
        {
            await base.OnPostAsync();
            CurrentUser = await _dbContext.Users.FindAsync(UserId);
            if (CurrentUser != null && CurrentUser.LastLogin != null)
            {
                ModelState.Remove("NewPassword");
                ModelState.Remove("ConfirmPassword");
            }
            if (!ModelState.IsValid)
            {
                return Page();
            }

            var user = await _dbContext.Users.FindAsync(UserId);

            if (user == null || user.TwoFactorCode != Code || user.TwoFactorCodeExpiration < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, "קוד האימות שגוי או פג תוקף.");
                return Page();
            }

            var isFirstTimeLogin = user.LastLogin == null;
            if (!string.IsNullOrEmpty(user.PendingEmail))
            {
                user.Email = user.PendingEmail;
                user.PendingEmail = null;
                user.EmailVerified = true;
            }
            if (isFirstTimeLogin)
            {
                user.LastLogin = DateTime.UtcNow;
                user.EmailVerified = true; // Also mark verified on first login
            }
            if (!string.IsNullOrWhiteSpace(NewPassword))
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, NewPassword!);
            }
            user.TwoFactorCode = null;
            user.TwoFactorCodeExpiration = null;

            // Record the successful login attempt
            // This updates the LastLogin table, ensuring the next login is not flagged as first-time.
            await _dbContext.SaveChangesAsync();

            var claims = new List<Claim>   // refresh Claims in case of new Email
                {
                    new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Role, user.IsAdmin ? "Admin" : "Member"),
                    new Claim(ClaimTypes.Email, user.Email) // This is now the NEW verified email
                };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var claimsPrincipal = new ClaimsPrincipal(claimsIdentity);
            // Clear the current user session/cookie (Logout), as we don't know to which Subject to login.
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, claimsPrincipal);

            if (isFirstTimeLogin)
            {
                TempData["SuccessMessage"] = "הגדרת החשבון הושלמה בהצלחה!";
                return RedirectToPage("/Index"); // New users go to home
            }
            else
            {
                TempData["SuccessMessage"] = "כתובת האימייל אומתה ועודכנה בהצלחה.";
                return RedirectToPage("/UpdatePage"); // Profile editors go back to profile
            }
        }
    }
}
