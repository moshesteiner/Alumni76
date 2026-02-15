using Alumni76.Data;
using Alumni76.Models;
using Alumni76.Pages.Common;
using Alumni76.Utilities;
using DocumentFormat.OpenXml.Drawing.Charts;
using DocumentFormat.OpenXml.Spreadsheet;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using static Alumni76.Pages.AdminPageModel;

namespace Alumni76.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class UserManagementModel : BasePageModel<UserManagementModel>
    {
        private readonly IPasswordHasher<User> _passwordHasher;

        [BindProperty(SupportsGet = true)]
        public FilterModel? FilterModel { get; set; }

        public List<User> UsersList { get; set; } = new();
        [BindProperty] // Required for the POST to capture the list of users
        public List<UserUpdateModel> Users { get; set; } = new();
        public List<UserDisplayModel>? DisplayUsers { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? Sort { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Dir { get; set; }

        public UserManagementModel(ApplicationDbContext dbContext, ILogger<UserManagementModel> logger,
            IPasswordHasher<User> passwordHasher, ITimeProvider timeProvider): base(dbContext, logger, timeProvider) 
        {
            _passwordHasher = passwordHasher;
        }

        public class UserDisplayModel
        {
            public int UserId { get; set; }
            public string? FirstName { get; set; }
            public string? LastName { get; set; }
            public string? MaidenName { get; set; }            
            public string? Class { get; set; }
            public string? Email { get; set; }
            public bool Active { get; set; }
            public bool IsAdmin { get; set; }
        }       
        public new async Task OnGetAsync()
        {           
            ModelState.Clear();
            await base.OnGetAsync();
            CheckForSpecialAdmin();

            SetFilterModel();

            var sortState = new List<string>();
            if (!string.IsNullOrEmpty(Sort))
            {
                sortState.Add($"{Sort}_{(Dir == "desc" ? "desc" : "asc")}");
            }
            ViewData["SortState"] = sortState;
            await LoadUsersAsync();
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
        private async Task LoadUsersAsync()
        {
            CheckForSpecialAdmin();

            IQueryable<User> query = _dbContext.Users;

            query = query.ApplyFilters(FilterModel!);
            query = ApplySort(query);

            var users = await query.AsNoTracking().ToListAsync();

            DisplayUsers = users.Select(u => new UserDisplayModel
            {
                UserId = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                MaidenName = u.MaidenName,
                Email = u.Email,
                Class = u.Class,               
                Active = u.Active,
                IsAdmin = u.IsAdmin
            }).ToList();
        }
        private IQueryable<User> ApplySort(IQueryable<User> query)
        {
            if (string.IsNullOrEmpty(Sort))
            {
                return query.OrderBy(u => u.Class).ThenBy(u => u.FirstName).ThenBy(u => u.LastName);
            }

            return Sort switch
            {
                "FirstName" => Dir == "desc" ? 
                        query.OrderByDescending(u => u.FirstName).ThenByDescending(u=>u.LastName) : 
                        query.OrderBy(u => u.FirstName).ThenBy(u=>u.LastName),

                "LastName" => Dir == "desc" ? 
                        query.OrderByDescending(u => u.LastName).ThenByDescending(u=>u.FirstName) : 
                        query.OrderBy(u => u.LastName).ThenBy(u=>u.FirstName),

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
        public async Task<IActionResult> OnPostUpdateUserAsync(int userId, bool isActive, bool isAdmin)
        {
            var user = await _dbContext.Users.FindAsync(userId);
            if (user == null) return NotFound();

            // Prevent the Special Admin from accidentally de-admin-ing themselves
            if (user.Email == specialAdminEmail && !isAdmin)
            {
                TempData["ErrorMessage"] = "לא ניתן לבטל הרשאת אדמין למשתמש על זה.";
                return RedirectToPage();
            }

            user.Active = isActive;
            user.IsAdmin = isAdmin;

            await _dbContext.SaveChangesAsync();
            TempData["SuccessMessage"] = $"הסטטוס של {user.FirstName} {user.LastName} עודכן.";

            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostEmergencyPasswordResetAsync()
        {
            // 1. Only target users who have NEVER logged in
            var usersToReset = await _dbContext.Users
                .Where(u => u.LastLogin == null)
                .ToListAsync();

            return null!;

            var exportData = new StringBuilder();
            // 2. Added ID column to help you identify duplicates in SQL
            exportData.AppendLine("Id,Name,Email,NewPassword");

            foreach (var user in usersToReset)
            {
                string newPassword = Guid.NewGuid().ToString().Substring(0, 6);
                user.PasswordHash = _passwordHasher.HashPassword(user, newPassword);

                // 3. Export with ID and the Excel-friendly password format
                exportData.AppendLine($"{user.Id},\"{user.FirstName} {user.LastName}\",{user.Email},\"{newPassword}\"");
            }

            await _dbContext.SaveChangesAsync();

            // 4. Hebrew-friendly encoding (UTF-8 with BOM)
            var encoding = new UTF8Encoding(true);
            var bytes = encoding.GetBytes(exportData.ToString());

            return File(bytes, "text/csv", "Alumni_Emergency_Export.csv");
        }
    }
}