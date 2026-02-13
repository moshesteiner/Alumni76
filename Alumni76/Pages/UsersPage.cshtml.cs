using Alumni76.Data;
using Alumni76.Models;
using Alumni76.Pages.Common;
using Alumni76.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;

[Authorize]  // only logged in users
public class UsersPageModel : BasePageModel<UsersPageModel>
{
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IEmailService _emailService;

    [BindProperty(SupportsGet = true)]
    public FilterModel? FilterModel { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Dir { get; set; }

    public List<UserDisplayModel>? DisplayUsers { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }
    [TempData]
    public string? ErrorMessage { get; set; }

    public UsersPageModel(
        ApplicationDbContext dbContext,
        ILogger<UsersPageModel> logger,
        IPasswordHasher<User> passwordHasher,
        IEmailService emailService,
        ITimeProvider timeProvider) : base(dbContext, logger, timeProvider)
    {
        _passwordHasher = passwordHasher;
        _emailService = emailService;
    }


    public class UserDisplayModel
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
        //
        //public string? Phone => Phone1; // Map 'Phone' to 'Phone1' if the view uses @user.Phone
        //public string? Role { get; set; }
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

    public async Task<IActionResult> OnPostResetSortAsync()
    {
        HttpContext.Session.Remove(FilterSessionKey);
        SetFilterModel();
        //HttpContext.Session.Remove(SortSessionKey);
        ViewData["SortState"] = null;
        await LoadUsersAsync();
        Sort = null;
        Dir = null;
        return RedirectToPage();
    }

    private IQueryable<User> ApplySort(IQueryable<User> query)
    {
        if (string.IsNullOrEmpty(Sort))
        {
            return query.OrderBy(u => u.Class).ThenBy(u => u.FirstName).ThenBy(u => u.LastName);
        }

        return Sort switch
        {
            "FirstName" => Dir == "desc" ? query.OrderByDescending(u => u.FirstName) : query.OrderBy(u => u.FirstName),
            "LastName" => Dir == "desc" ? query.OrderByDescending(u => u.LastName) : query.OrderBy(u => u.LastName),
            "Class" => Dir == "desc" ? query.OrderByDescending(u => u.Class) : query.OrderBy(u => u.Class),
            // Add a default case to satisfy the compiler
            _ => query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName)
        };
    }

    private async Task LoadUsersAsync()
    {
        CheckForSpecialAdmin();

        IQueryable<User> query = _dbContext.Users.Where(u => u.Active);

        query = query.ApplyFilters(FilterModel!);
        query = ApplySort(query);

        var users = await query.AsNoTracking().ToListAsync();

        DisplayUsers = users.Select(u => new UserDisplayModel
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
            Active = u.Active
        }).ToList();
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

    public IActionResult OnPostApplyFilter()
    {
        CheckForSpecialAdmin();

        if (FilterModel != null)
            FilterModel.DisplaySubjectSearch = IsSpecialAdmin;

        HttpContext.Session.SetString(FilterSessionKey, JsonSerializer.Serialize(FilterModel));

        return RedirectToPage(new { Sort, Dir });
    }

}