using Alumni76.Data;
using Alumni76.Models;
using Alumni76.Pages.Common;
using Alumni76.Utilities;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
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

            Users = await query.Select(u => new UserUpdateModel
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
                            Participate = _dbContext.Participates.Any(p => p.UserId == u.Id)
                        })
                        .OrderBy(u => u.Class)
                        .ToListAsync();

            // Counting Users
            //CountAllUsers = await _dbContext.Users.CountAsync();
            //CountActiveUsers = await _dbContext.Users.CountAsync(u => u.Active);
            CountAllUsers = await query.CountAsync();
            CountActiveUsers = await query.CountAsync(u => u.Active);
            CountParticipants = await _dbContext.Participates.CountAsync();          
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
            user.Phone1 = userUpdate.Phone1;
            user.Phone2 = userUpdate.Phone2;
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
        public class UserUpdateModel
        {
            public int UserId { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? MaidenName { get; set; }
            public string? NickName { get; set; }
            public string? Class { get; set; }
            public string? Email { get; set; }
            public string? Phone1 { get; set; }
            public string? Phone2 { get; set; }
            public string? Address { get; set; }
            public bool Active { get; set; }
            public bool Participate { get; set; }
        }
    }
}