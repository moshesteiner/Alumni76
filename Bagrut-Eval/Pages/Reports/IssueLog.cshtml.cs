using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Bagrut_Eval.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Bagrut_Eval.Pages.Common;
using Microsoft.AspNetCore.Http;
using System.Text.Json;

namespace Bagrut_Eval.Pages.Reports
{
    [Authorize(Roles = "Admin")] // Only allow Admins to view this report
    public class IssueLogModel : BasePageModel<IssueLogModel>
    {
        [BindProperty(SupportsGet = true)]
        public int? SelectedExamId { get; set; }

        public List<IssueLog> IssueLogs { get; set; } = new List<IssueLog>(); // Initialize to prevent null reference
        [BindProperty(SupportsGet = true)]
        public FilterModel? FilterModel { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? Sort { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Dir { get; set; }
        public new const string FilterSessionKey = "IssueLogFilterState";    // Session variable to store filters

        public IssueLogModel(ApplicationDbContext context, ILogger<IssueLogModel> logger, ITimeProvider timeProvider) :
                    base(context, logger, timeProvider)
        {
        }
        public async Task OnGetAsync(int? selectedExamId)
        {
            string? json = HttpContext.Session.GetString(FilterSessionKey);
            if (json != null)
            {
                FilterModel = JsonSerializer.Deserialize<FilterModel>(json) ?? FilterModel;
            }
            SetFilterModel();

            ModelState.Clear();
            SelectedExamId = selectedExamId;

            await LoadReportDataAsync(selectedExamId);

            var sortState = new List<string>();
            if (!string.IsNullOrEmpty(Sort))
            {
                sortState.Add($"{Sort}_{(Dir == "desc" ? "desc" : "asc")}");
            }
            ViewData["SortState"] = sortState;
        }

        public async Task<IActionResult> OnPostFilterAsync()
        {
            await LoadReportDataAsync(SelectedExamId);
            return RedirectToPage(new { SelectedExamId = SelectedExamId, Sort = Sort, Dir = Dir });
        }
        public IActionResult OnPostApplyFilter()
        {
            string json = JsonSerializer.Serialize(FilterModel);
            HttpContext.Session.SetString(FilterSessionKey, json);

            // Redirect back to GET
            return RedirectToPage(new
            {
                SelectedExamId = SelectedExamId,
                Sort = Sort,
                Dir = Dir
            });
        }
        public new async Task<IActionResult> OnPostAsync()
        {
            await base.OnPostAsync();
            await LoadReportDataAsync(SelectedExamId);
            return Page();
        }

        private async Task LoadReportDataAsync(int? selectedExamId)
        {
            // Load active exams for the dropdown (No Change)
            AvailableExams = new SelectList(await _dbContext.Exams
                                                    .Where(e => e.Active)
                                                    .OrderBy(e => e.ExamTitle)
                                                    .ToListAsync(), "Id", "ExamTitle");

            IQueryable<Issue> issuesQuery = _dbContext.Issues
                                                .AsNoTracking();

            if (SelectedExamId.HasValue && SelectedExamId.Value > 0)
            {
                issuesQuery = issuesQuery.Where(i => i.ExamId == SelectedExamId.Value);
            }

            IQueryable<IssueLog> issueLogsQuery = issuesQuery.SelectMany(i => i.IssueLogs);

            // Now, apply all the Includes to the issueLogsQuery (No Change)
            issueLogsQuery = issueLogsQuery
                .Include(il => il.Issue)
                    .ThenInclude(i => i!.Part)
                .Include(il => il.User);

            issueLogsQuery = issueLogsQuery.ApplyFilters(FilterModel!, HttpContext, true); // true->check in question field
            issueLogsQuery = ApplySort(issueLogsQuery);

            IssueLogs = await issueLogsQuery.ToListAsync();
        }
        private IQueryable<IssueLog> ApplySort(IQueryable<IssueLog> query)
        {
            if (string.IsNullOrEmpty(Sort))
            {
                // Default sort order if no sort is specified
                return query = query.OrderBy(i => i.Issue!.QuestionNumber).ThenBy(i => i.Issue!.Part!.QuestionPart);
            }
            else
            {
                switch (Sort)
                {
                    case "QuestionNumber":
                        return (Dir == "desc") ?
                            query.OrderByDescending(i => i.Issue!.QuestionNumber).ThenByDescending(i => i.Issue!.Part!.QuestionPart) :
                            query.OrderBy(i => i.Issue!.QuestionNumber).ThenBy(i => i.Issue!.Part!.QuestionPart);
                    case "Status":
                        return (Dir == "desc") ?
                            query.OrderByDescending(i => i.Issue!.Status) :
                            query.OrderBy(i => i.Issue!.Status);
                    case "Date":
                        return (Dir == "desc") ?
                            query.OrderByDescending(i => i.LogDate) :
                            query.OrderBy(i => i.LogDate);
                    case "User":
                        return (Dir == "desc") ?
                           query.OrderByDescending(i => i.User!.FirstName).ThenByDescending(i=>i.User!.LastName) :
                           query.OrderBy(i => i.User!.FirstName).ThenBy(i => i.User!.LastName);
                    default:
                        return query;
                }
            }
        }
        public async Task<IActionResult> OnPostResetSortAsync()
        {
            // restore filter status
            string json = JsonSerializer.Serialize(FilterModel);
            HttpContext.Session.SetString(FilterSessionKey, json);

            HttpContext.Session.Remove("IssuesSortState");
            ViewData["SortState"] = null;
            await LoadReportDataAsync(SelectedExamId);
            Sort = null;
            Dir = null;
            return RedirectToPage(new { SelectedExamId = SelectedExamId});
        }
        private void SetFilterModel()
        {
            if (FilterModel == null)
                FilterModel = new FilterModel();
                FilterModel.DisplayDescriptionSearch = true;
                FilterModel.DisplayFilterDate = true;
                FilterModel.DisplayUserNameSearch = true;
                FilterModel.DisplayShowClosed = true;
                FilterModel.DisplayShowNewerThanLastLogin = true;
                FilterModel.DisplayShowActiveOrOpen = false;
        }
    }
}