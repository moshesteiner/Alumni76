// File: Pages/Reports/TablesRawModel.cshtml.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims; // For ClaimTypes
using System;
using Bagrut_Eval.Pages.Common;
using Bagrut_Eval.Utilities;

namespace Bagrut_Eval.Pages.Reports
{
    [Authorize(Roles = "Senior, Admin")]
    public class TablesRawModel : BasePageModel<TablesRawModel>
    {
        public TablesRawModel(ApplicationDbContext dbContext, ILogger<TablesRawModel> logger, ITimeProvider timeProvider) :
            base(dbContext, logger, timeProvider)
        {
        }

        public IList<User> Users { get; set; } = default!;
        public IList<Exam> Exams { get; set; } = default!;
        public IList<Part> Parts { get; set; } = default!;
        public IList<Issue> Issues { get; set; } = default!;
        public IList<Drawing> Drawings { get; set; } = default!;
        public IList<Metric> Metrics { get; set; } = default!;
        public IList<LastLogin?> LastLogins { get; set; } = default!;

        // Add the FilterSortModel property to the page model
        [BindProperty(SupportsGet = true)]
        public FilterModel FilterSortModel { get; set; } = new FilterModel();

        public new async Task OnGetAsync()
        {
            await base.OnGetAsync();
            await LoadDataAsync();
        }

        public new async Task<IActionResult> OnPostAsync()
        {
            await base.OnPostAsync();
            // When the form is submitted from the modal, this handler runs.
            // The FilterSortModel property is automatically populated.
            await LoadDataAsync();
            return Page();
        }

        private async Task LoadDataAsync()
        {
            // Fetch Users
            Users = await _dbContext.Users.Include(u => u.UserSubjects).ThenInclude(us => us.Subject)
                            .OrderBy(u => u.FirstName)
                                .ThenBy(u => u.LastName)
                            .ToListAsync();

            // Fetch Exams
            Exams = await _dbContext.Exams.ToListAsync();

            // Fetch Parts
            Parts = await _dbContext.Parts
                                    .Include(p => p.Exam)
                                    .Include(p => p.Issues)
                                    .ToListAsync();

            // Fetch Issues and apply filtering/sorting
            //IQueryable<Issue> issuesQuery = _dbContext.Issues
            //                                        .Include(i => i.Part)
            //                                            .ThenInclude(p => p!.Exam)
            //                                        .Include(i => i.User)
            //                                        .Include(i => i.FinalAnswer)
            //                                            .ThenInclude(fa => fa!.Senior)                                                        
            //                                        .AsNoTracking();



            //Issues = await issuesQuery.ToListAsync();


            // 1. Define an Anonymous Type (or better, a DTO/ViewModel) to hold the results.
            // 2. Use .Select() to project data from the joined entities.

            //var issuesQuery = await _dbContext.Issues
            //    .Select(i => new
            //    {
            //        IssueId = i.Id,
            //        ExamTitle = i.Exam!.ExamTitle, // Joins to Exams
            //        i.QuestionNumber,
            //        i.Description,
            //        i.OpenDate,
            //        i.Status,
            //        i.CloseDate,

            //        // Joins to Users (opener)
            //        UserName = i.User!.FirstName,
            //        UserLast = i.User!.LastName,

            //        // Joins to Answers (FinalAnswer) and then to Users (Senior)
            //        SeniorName = i.FinalAnswer!.Senior!.FirstName,
            //        SeniorLast = i.FinalAnswer!.Senior!.LastName,
            //        Content = i.FinalAnswer.Content
            //    })
            //    .AsNoTracking()
            //    .ToListAsync(); // Execute the query

            // Assuming this code is inside your PageModel's data-fetching method (e.g., OnGetAsync)

            // 1. Build the query using Include/ThenInclude to ensure all related data is loaded.
            var issuesQuery = _dbContext.Issues
                .Include(i => i.Exam)
                .Include(i => i.User)
                .Include(i => i.FinalAnswer)
                    .ThenInclude(fa => fa!.Senior)
                .Include(i => i.Part)
                //.Where(i => i.ExamId != null && i.FinalAnswerId != null && i.UserId != null)
                .AsNoTracking(); // Good practice for read-only data

            Issues = await issuesQuery.ToListAsync();

            Drawings = await _dbContext.Drawings
                                    .Include(i => i.Issue)
                                    .ToListAsync();

            // The rest of your data loading remains the same
            Metrics = await _dbContext.Metrics.ToListAsync();
            LastLogins = _dbContext.LastLogins
                .Include(ll => ll.User)
                .AsEnumerable()
                .GroupBy(ll => ll.UserId)
                .Select(group => group.OrderByDescending(ll => ll.LoginDate).FirstOrDefault())
                .OrderByDescending(ll => ll!.LoginDate)
                .Where(ll => ll != null)
                .ToList();
        }
    }
}