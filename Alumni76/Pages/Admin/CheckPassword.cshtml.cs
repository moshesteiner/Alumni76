using Alumni76.Data;
using Alumni76.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Alumni76.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class CheckPasswordModel : PageModel
    {
        private readonly ApplicationDbContext _dbContext;
        private readonly IPasswordHasher<User> _passwordHasher;

        public CheckPasswordModel(ApplicationDbContext dbContext, IPasswordHasher<User> passwordHasher)
        {
            _dbContext = dbContext;
            _passwordHasher = passwordHasher;
        }

        [BindProperty]
        public string Email { get; set; } = string.Empty;
        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public string? ResultMessage { get; set; }
        public bool? IsSuccess { get; set; }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
            {
                ResultMessage = "נא להזין אימייל וסיסמה לבדיקה.";
                return Page();
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == Email);
            if (user == null)
            {
                ResultMessage = "משתמש לא נמצא במערכת.";
                IsSuccess = false;
                return Page();
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, Password);

            if (result == PasswordVerificationResult.Success)
            {
                ResultMessage = $"הסיסמה תקינה עבור {user.FirstName} {user.LastName}!";
                IsSuccess = true;
            }
            else
            {
                ResultMessage = "הסיסמה שגויה.";
                IsSuccess = false;
            }

            return Page();
        }
    }
}
