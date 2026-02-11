using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Bagrut_Eval.Pages.Common;
using Bagrut_Eval.Utilities;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;

namespace Bagrut_Eval.Pages
{
    [Authorize(Roles = "Senior, Admin")]
    public class AnswerPageModel : BasePageModel<AnswerPageModel>
    {
        [BindProperty(SupportsGet = true)]
        public int? SelectedExamId { get; set; }

        [BindProperty(SupportsGet = true)]
        public FilterModel? FilterModel { get; set; }
        public string? SelectedExamTitle { get; set; }
        public List<Issue>? Issues { get; set; }
        [BindProperty(SupportsGet = true)]
        public string? Sort { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? Dir { get; set; }

        public int CurrentLoggedInUserId { get; set; }
        public new const string FilterSessionKey = "AnswerPageFilterState";    // Session variable to store filters
        public const string SortSessionKey = "IssuesSortState";

        public AnswerPageModel(ApplicationDbContext context, ILogger<AnswerPageModel> logger, 
                               ITimeProvider timeProvider):base(context, logger, timeProvider)
        {
        }

        public async Task OnGetAsync(int? selectedExamId, string? sort, string? dir, string? secondarySort)
        {
            ModelState.Clear();
            await base.OnGetAsync();
            SelectedExamId = selectedExamId;
            string? json = HttpContext.Session.GetString(FilterSessionKey);
            if (json != null)
            {
                FilterModel = JsonSerializer.Deserialize<FilterModel>(json) ?? FilterModel;
            }
            else
            {
                SetFilterModel();
            }

            var sortState = HttpContext.Session.Get<List<string>>(SortSessionKey) ?? new List<string>();

            if (!string.IsNullOrEmpty(sort) && !string.IsNullOrEmpty(dir))
            {
                string primarySortKey = $"{sort}_{dir}";

                sortState.RemoveAll(s => s.StartsWith(sort));
                if (!string.IsNullOrEmpty(secondarySort))
                {
                    sortState.RemoveAll(s => s.StartsWith(secondarySort));
                }
                sortState.Insert(0, primarySortKey);
                // If a secondary sort is provided, add it as well
                if (!string.IsNullOrEmpty(secondarySort))
                {
                    string secondarySortKey = $"{secondarySort}_{dir}";
                    sortState.Insert(1, secondarySortKey);
                }
            }

            // Trim the list to a reasonable size for multi-column sorting
            // For example, to allow up to two levels of sorting (primary and secondary)
            while (sortState.Count > 2)
            {
                sortState.RemoveAt(sortState.Count - 1);
            }

            // Save the updated sort state back to the session
            HttpContext.Session.Set(SortSessionKey, sortState);

            // Store sort state in ViewData so the view can access it
            ViewData["SortState"] = sortState;

            await LoadDataAsync(SelectedExamId);
        }

        public IActionResult OnPostResetSortAsync()
        {
            HttpContext.Session.Remove(FilterSessionKey);
            SetFilterModel();
            HttpContext.Session.Remove(SortSessionKey);
            ViewData["SortState"] = null;
            Sort = null;
            Dir = null;
            return RedirectToPage(new { SelectedExamId = SelectedExamId });
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
            string json = JsonSerializer.Serialize(FilterModel);
            HttpContext.Session.SetString(FilterSessionKey, json);
        }

        public IActionResult OnPostFilter()
        {
            string? sortParam = null;
            string? dirParam = null;

            var sortState = HttpContext.Session.Get<List<string>>(SortSessionKey);

            if (sortState != null && sortState.Any())
            {
                // Get the primary sort key (e.g., "OpenDate_desc")
                var primarySort = sortState.First();
                var primarySortParts = primarySort.Split('_');

                // Assign the values to the parameters for the redirect
                sortParam = primarySortParts[0];
                dirParam = primarySortParts.Length > 1 ? primarySortParts[1] : "asc";
            }

            // 3. Redirect to the GET handler with only the minimum required parameters.
            return RedirectToPage(new
            {
                SelectedExamId = SelectedExamId,
                Sort = sortParam,
                Dir = dirParam
            });
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

        public async Task<IActionResult> OnPostReopenIssueAsync(int issueId, int? selectedExamId)
        {
            var originalIssue = await _dbContext.Issues
                                              .FirstOrDefaultAsync(i => i.Id == issueId);

            if (originalIssue == null)
            {
                TempData["ErrorMessage"] = "Issue not found.";
                await LoadDataAsync(selectedExamId);
                return Page();
            }

            CurrentLoggedInUserId = GetCurrentUserId();
            if (CurrentLoggedInUserId == 0)
            {
                TempData["ErrorMessage"] = "You must be logged in to reopen issues.";
                await LoadDataAsync(selectedExamId);
                return Page();
            }

            if (originalIssue.Status == IssueStatus.Closed)
            {
                originalIssue.Status = IssueStatus.Open;
                originalIssue.CloseDate = null;

                _dbContext.Entry(originalIssue).State = EntityState.Modified;

                var issueLog = new IssueLog
                {
                    IssueId = originalIssue.Id,
                    UserId = CurrentLoggedInUserId,
                    LogDate = _timeProvider.Now,
                    Description = "נפתח מחדש"
                };
                _dbContext.IssueLogs.Add(issueLog);

                try
                {
                    await _dbContext.SaveChangesAsync();
                    TempData["SuccessMessage"] = "נפתח מחדש בהצלחה";
                }
                catch (DbUpdateConcurrencyException)
                {
                    TempData["ErrorMessage"] = "שגיאת עומס: עודכן על ידי משתמש אחר. אנא רענן ונסה שוב.";
                }
                catch (Exception ex)
                {
                    TempData["ErrorMessage"] = $"שגיאה בפתיחת השאלה מחדש: {ex.Message}";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "הנושא כבר פתוח";
            }

            await LoadDataAsync(selectedExamId);
            string json = JsonSerializer.Serialize(FilterModel);
            HttpContext.Session.SetString(FilterSessionKey, json);
            return RedirectToPage("/ResolvePage", new { issueId = originalIssue.Id, selectedExamId = selectedExamId });
        }

        private async Task LoadDataAsync(int? examId)
        {
            SelectedExamId = examId;
            CurrentLoggedInUserId = GetCurrentUserId();

            //AvailableExams = new SelectList(await _dbContext.Exams
            //                                             .Where(e => e.Active && e.SubjectId == SubjectId)
            //                                             .OrderBy(e => e.ExamTitle)
            //                                             .ToListAsync(), "Id", "ExamTitle");
          
            AvailableExams = new SelectList(await _dbContext.AllowedExams
                                                         .Where(ae => ae.UserId == CurrentLoggedInUserId)
                                                         .Join(_dbContext.Exams.Where(e => e.SubjectId == SubjectId),
                                                               ae => ae.ExamId,
                                                               e => e.Id,
                                                               (ae, e) => e)
                                                         .Where(e => e.Active)
                                                         .OrderBy(e => e.ExamTitle)
                                                         .Select(e => new 
                                                         {
                                                             Id = e.Id.ToString(),
                                                             ExamTitle = e.ExamTitle ?? ""
                                                         })
                                                         .ToListAsync(), "Id", "ExamTitle"); 

            if (SelectedExamId.HasValue && SelectedExamId.Value > 0)
            {
                SelectedExamTitle = (await _dbContext.Exams.FirstOrDefaultAsync(e => e.Id == SelectedExamId.Value))?.ExamTitle;

                IQueryable<Issue> issuesQuery = _dbContext.Issues
                                                  .Include(i => i.Part)
                                                  .Include(i => i.User)
                                                  .Include(i => i.FinalAnswer)
                                                      .ThenInclude(fa => fa!.Senior)
                                                  .Include(i => i.Drawings)
                                                  .Where(i => i.ExamId == SelectedExamId.Value);

                // --- Start of sorting
                var sortState = HttpContext.Session.Get<List<string>>(SortSessionKey) ?? new List<string>();
                bool isFirstSort = true;

                if (sortState.Any())
                {
                    foreach (var sortKey in sortState)
                    {
                        var parts = sortKey.Split('_');
                        var columnName = parts[0];
                        var direction = parts.Length > 1 ? parts[1] : "asc";

                        issuesQuery = issuesQuery.ApplyDynamicSort(columnName, direction, isFirstSort);
                        isFirstSort = false;
                    }
                }
                else
                {
                    issuesQuery = issuesQuery.OrderByDescending(i => i.OpenDate);
                }
                // --- End of sorting

                issuesQuery = issuesQuery.ApplyFilters(FilterModel!, HttpContext);

                Issues = await issuesQuery.ToListAsync();
            }
            else
            {
                Issues = new List<Issue>();
            }
        }

        private int GetCurrentUserId()
        {
            if (User.Identity!.IsAuthenticated)
            {
                var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int userId))
                {
                    return userId;
                }
            }
            return 0;
        }
    }
}