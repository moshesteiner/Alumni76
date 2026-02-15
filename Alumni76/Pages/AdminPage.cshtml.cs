using Alumni76.Data;
using Alumni76.Models;
using Alumni76.Pages.Common;
using Alumni76.Utilities;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;

namespace Alumni76.Pages
{
    [Authorize(Roles = "Admin")] // Ensures only Admins can access
    public class AdminPageModel : BasePageModel<AdminPageModel>
    {
        private readonly IPasswordHasher<User> _passwordHasher;
        private readonly IEmailService _emailService;

        [TempData]
        public string? SuccessMessage { get; set; }

        [BindProperty] // Required for the POST to capture the list of users
        public List<UserUpdateModel> Users { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public FilterModel? FilterModel { get; set; }
        public int CountAllUsers { get; set; }
        public int CountActiveUsers { get; set; }
        public int CountParticipants { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Sort { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Dir { get; set; }

        public AdminPageModel(ApplicationDbContext dbContext, ILogger<AdminPageModel> logger,
            IPasswordHasher<User> passwordHasher, IEmailService emailService, ITimeProvider timeProvider)
            : base(dbContext, logger, timeProvider)
        {
            _passwordHasher = passwordHasher;
            _emailService = emailService;
        }
        public new async Task OnGetAsync()
        {
            ModelState.Clear();
            await base.OnGetAsync();
            SetFilterModel();
            var query = _dbContext.Users.AsQueryable();
            if (FilterModel != null)
            {
                query = query.ApplyFilters(FilterModel);
            }

            var sortState = new List<string>();
            if (!string.IsNullOrEmpty(Sort))
            {
                sortState.Add($"{Sort}_{(Dir == "desc" ? "desc" : "asc")}");
            }
            ViewData["SortState"] = sortState;

            var usersQuery = query.Select(u => new UserUpdateModel
            {
                UserId = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                MaidenName = u.MaidenName,
                NickName = u.NickName,
                Email = u.Email,
                Class = u.Class,
                Phone1 = u.Phone1,
                Phone2 = u.Phone2,
                Address = u.Address,
                Active = u.Active,
                LastLogin = u.LastLogin,
                Participate = _dbContext.Participates.Any(p => p.UserId == u.Id)
            });
            usersQuery = ApplySort(usersQuery);
            Users = await usersQuery.ToListAsync();

            // Counting Users
            CountAllUsers = await _dbContext.Users.CountAsync();
            CountActiveUsers = await query.CountAsync(u => u.Active);
            CountParticipants = await _dbContext.Participates.CountAsync();
        }
        //private IQueryable<User> ApplySort(IQueryable<User> query)
        private IQueryable<UserUpdateModel> ApplySort(IQueryable<UserUpdateModel> query)
        {
            if (string.IsNullOrEmpty(Sort))
            {
                return query.OrderBy(u => u.Class).ThenBy(u => u.FirstName).ThenBy(u => u.LastName);
            }

            return Sort switch
            {
                "FirstName" => Dir == "desc" ?
                        query.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName) :
                        query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName),

                "LastName" => Dir == "desc" ?
                        query.OrderByDescending(u => u.LastName).ThenByDescending(u => u.FirstName) :
                        query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName),

                "Class" => Dir == "desc" ?
                        query.OrderByDescending(u => u.Class).ThenByDescending(u => u.FirstName).ThenByDescending(u => u.LastName) :
                        query.OrderBy(u => u.Class).ThenBy(u => u.FirstName).ThenBy(u => u.LastName),

                "LastLogin" => Dir == "desc" ?
                        query.OrderByDescending(u => u.LastLogin).ThenByDescending(u => u.FirstName).ThenByDescending(u => u.LastName) :
                        query.OrderBy(u => u.LastLogin).ThenBy(u => u.FirstName).ThenBy(u => u.LastName),

                // Add a default case to satisfy the compiler
                _ => query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
            };
        }
        private void SetFilterModel()
        {
            string? json = HttpContext.Session.GetString(FilterSessionKey);
            if (json != null)
            {
                FilterModel = JsonSerializer.Deserialize<FilterModel>(json) ?? new FilterModel();
            }
            else
            {
                FilterModel = new FilterModel
                {
                    ShowActiveOrOpen = true,
                    DisplayDescriptionSearch = false,
                    DisplayFilterDate = false,
                    DisplayShowActiveOrOpen = false,
                    DisplayShowClosed = false,
                    DisplayShowNewerThanLastLogin = false,
                    DisplaySubjectSearch = false,
                    DisplayUserNameSearch = true
                };
            }
            json = JsonSerializer.Serialize(FilterModel);
            HttpContext.Session.SetString(FilterSessionKey, json);
        }
        public async Task<IActionResult> OnPostResetSortAsync()
        {
            HttpContext.Session.Remove(FilterSessionKey);
            SetFilterModel();
            ViewData["SortState"] = null;
            await OnGetAsync();
            return RedirectToPage();
        }
        public IActionResult OnPostApplyFilter()
        {
            CheckForSpecialAdmin();

            if (FilterModel != null)
                FilterModel.DisplaySubjectSearch = IsSpecialAdmin;

            HttpContext.Session.SetString(FilterSessionKey, JsonSerializer.Serialize(FilterModel));

            return RedirectToPage();
        }

        // Change the method signature to take a single model
        public async Task<IActionResult> OnPostSaveUserAsync([FromForm] UserUpdateModel userUpdate)
        {
            if (!ModelState.IsValid)
            {
                var errors = string.Join(" | ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                TempData["ErrorMessage"] = $"שגיאת נתונים: {errors}";
                TempData["ErrorMessage"] = $"נתונים חסרים או לא תקינים: {errors}";
                return RedirectToPage();
            }

            if (userUpdate == null || userUpdate.UserId == 0) return BadRequest();

            var user = await _dbContext.Users.FindAsync(userUpdate.UserId);
            if (user == null) return NotFound();

            // Check if email already exists for another user
            if (await _dbContext.Users.AnyAsync(u => u.Email == userUpdate.Email && u.Id != userUpdate.UserId))
            {
                TempData["ErrorMessage"] = $"האימייל {userUpdate.Email} כבר קיים במערכת.";
                return RedirectToPage();
            }

            // Map updated fields
            user.FirstName = userUpdate.FirstName ?? "";
            user.LastName = userUpdate.LastName ?? "";
            user.MaidenName = userUpdate.MaidenName;
            user.NickName = userUpdate.NickName;
            user.Email = userUpdate.Email ?? "";
            user.Class = userUpdate.Class;
            user.Phone1 = FormatPhoneNumber(userUpdate.Phone1);
            user.Phone2 = FormatPhoneNumber(userUpdate.Phone2);
            user.Address = userUpdate.Address;

            // PREVENT SELF-DEACTIVATION:
            // Only update Active status if the user being edited is NOT the person logged in
            if (user.Id != GetCurrentUserId())
            {
                user.Active = userUpdate.Active;
            }
            else if (userUpdate.Active == false)
            {
                TempData["ErrorMessage"] = "אינך יכול לבטל את הסטטוס הפעיל של עצמך.";
            }

            // Handle Participation Table
            var existingParticipate = await _dbContext.Participates.FirstOrDefaultAsync(p => p.UserId == userUpdate.UserId);

            if (userUpdate.Participate && existingParticipate == null)
            {
                // Add if checked but not in table
                _dbContext.Participates.Add(new Participate { UserId = userUpdate.UserId });
            }
            else if (!userUpdate.Participate && existingParticipate != null)
            {
                // Remove if unchecked but exists in table
                _dbContext.Participates.Remove(existingParticipate);
            }

            await _dbContext.SaveChangesAsync();

            TempData["SuccessMessage"] = $"השינויים עבור {user.FirstName} נשמרו בהצלחה.";
            return RedirectToPage();
        }
        private int GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            int.TryParse(userIdClaim, out int currentUserId);
            return currentUserId;
        }

        public async Task<IActionResult> OnPostResetPasswordAsync(int userId)
        {
            var userToReset = await _dbContext.Users.FindAsync(userId);
            if (userToReset == null) return RedirectToPage();

            string newTempPassword = /*"TempP@ss" +*/ Guid.NewGuid().ToString().Substring(0, 6);
            userToReset.PasswordHash = _passwordHasher.HashPassword(userToReset, newTempPassword);

            await _dbContext.SaveChangesAsync();
            await _emailService.ResetPasswordEmailAsync(userToReset.Email!, userToReset.FirstName!, newTempPassword);

            SuccessMessage = $"הסיסמה עבור {userToReset.FirstName} אופסה ל: {newTempPassword}";
            return RedirectToPage();
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
        public class UserUpdateModel
        {
            public int UserId { get; set; }
            [Required(ErrorMessage = "שם פרטי הוא שדה חובה")]
            public string? FirstName { get; set; }
            [Required(ErrorMessage = "שם משפחה הוא שדה חובה")]
            public string? LastName { get; set; }
            public string? MaidenName { get; set; }
            public string? NickName { get; set; }
            public string? Class { get; set; }
            [Required(ErrorMessage = "אימייל הוא שדה חובה")]
            [EmailAddress(ErrorMessage = "כתובת אימייל לא תקינה")]
            public string? Email { get; set; }
            [Required(ErrorMessage = "חובה להזין מספר טלפון")]
            [RegularExpression(@"^(\+?\d{1,3}[- ]?)?\(?(\d{3})\)?[- ]?(\d{3,4})[- ]?(\d{4})$|^0?(\d{2,3})[\s-]?(\d{3})[\s-]?(\d{4})$", ErrorMessage = "פורמט לא תקין")]
            public string? Phone1 { get; set; }
            public string? Phone2 { get; set; }
            public string? Address { get; set; }
            public bool Active { get; set; }
            public DateTime? LastLogin { get; set; }
            public bool Participate { get; set; }
        }
    }
}