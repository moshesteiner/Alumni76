using Alumni76.Data;
using Alumni76.Models;
using Alumni76.Pages.Common;
using Alumni76.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

[Authorize]
public class ParticipatePageModel : BasePageModel<ParticipatePageModel>
{
    [BindProperty(SupportsGet = true)]
    public FilterModel? FilterModel { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Sort { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Dir { get; set; }

    public List<UserDisplayModel>? DisplayUsers { get; set; }

    public ParticipatePageModel(
        ApplicationDbContext dbContext,
        ILogger<ParticipatePageModel> logger,
        ITimeProvider timeProvider) : base(dbContext, logger, timeProvider)
    {
    }

    public class UserDisplayModel
    {
        public int UserId { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? MaidenName { get; set; }
        public string? NickName { get; set; }
        public string? Class { get; set; }
    }

    public new async Task OnGetAsync()
    {
        await base.OnGetAsync();
        SetFilterModel();

        var sortState = new List<string>();
        if (!string.IsNullOrEmpty(Sort)) sortState.Add($"{Sort}_{(Dir == "desc" ? "desc" : "asc")}");
        ViewData["SortState"] = sortState;

        await LoadParticipatingUsersAsync();
    }

    private FilterModel GetDefaultFilter() => new FilterModel { DisplayUserNameSearch = true };

    private async Task LoadParticipatingUsersAsync()
    {
        IQueryable<User> query = _dbContext.Users.Where(u => u.Active);
        // Inner Join
        var joinedQuery = from u in query
                          join p in _dbContext.Participates on u.Id equals p.UserId
                          select u;

        joinedQuery = joinedQuery.ApplyFilters(FilterModel!);
        joinedQuery = ApplySort(joinedQuery);

        // 4. Project to the Display Model
        DisplayUsers = await joinedQuery
            .AsNoTracking()
            .Select(u => new UserDisplayModel
            {
                UserId = u.Id,
                FirstName = u.FirstName,
                LastName = u.LastName,
                MaidenName = u.MaidenName,
                NickName = u.NickName,
                Class = u.Class
            })
            .ToListAsync();
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
        await LoadParticipatingUsersAsync();
        Sort = null;
        Dir = null;
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
    private IQueryable<User> ApplySort(IQueryable<User> query)
    {
        if (string.IsNullOrEmpty(Sort))
            return query.OrderBy(u => u.Class).ThenBy(u => u.FirstName);

        return Sort switch
        {
            "FirstName" => Dir == "desc" ? query.OrderByDescending(u => u.FirstName) : query.OrderBy(u => u.FirstName),
            "LastName" => Dir == "desc" ? query.OrderByDescending(u => u.LastName) : query.OrderBy(u => u.LastName),
            "Class" => Dir == "desc" ? query.OrderByDescending(u => u.Class) : query.OrderBy(u => u.Class),
            _ => query.OrderBy(u => u.FirstName)
        };
    }
}