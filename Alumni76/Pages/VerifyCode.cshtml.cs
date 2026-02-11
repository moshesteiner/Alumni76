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
                return Page();
            }
            return RedirectToPage("/Index");
        }

        public new async Task<IActionResult> OnPostAsync()
        {
            await base.OnPostAsync();
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

            // Update the password and clear the 2FA code
            user.PasswordHash = _passwordHasher.HashPassword(user, NewPassword!);
            user.TwoFactorCode = null;
            user.TwoFactorCodeExpiration = null;

            // Record the successful login attempt
            // This updates the LastLogin table, ensuring the next login is not flagged as first-time.
            await _dbContext.SaveChangesAsync();

            // Clear the current user session/cookie (Logout), as we don't know to which Subject to login.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            TempData["SuccessMessage"] = "הסיסמה עודכנה בהצלחה! אנא התחבר מחדש עם הסיסמה החדשה";

            return RedirectToPage("/Index");
        }
    }
}
