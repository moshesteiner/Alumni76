using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bagrut_Eval.Pages.Common;

namespace Bagrut_Eval.Pages.Reports
{
    [Authorize(Roles = "Admin")] // Only Admins can view this report
    public class UserLogModel : BasePageModel<UserLogModel>
    {
        [BindProperty(SupportsGet = true)]
        public int? SelectedUserId { get; set; }

        public SelectList? AvailableUsers { get; set; }

        public List<UserLog> UsersLogs { get; set; } = new List<UserLog>();

        public Dictionary<string, string> InitiatorNames { get; set; } = new Dictionary<string, string>();

        public string? SelectedUserStatus { get; set; }
        public string? SelectedUserRole { get; set; }

        public UserLogModel(ApplicationDbContext context, ILogger<UserLogModel> logger, ITimeProvider timeProvider) : base(context, logger, timeProvider)
        {
        }

        public async Task OnGetAsync(int? selectedUserId)
        {
            await LoadReportDataAsync(selectedUserId);
        }

        public async Task<IActionResult> OnPostFilterAsync()
        {
            await LoadReportDataAsync(SelectedUserId);
            return Page();
        }

        private async Task LoadReportDataAsync(int? selectedUserId)
        {
            SelectedUserId = selectedUserId;

            // Populate the dropdown with {firstName} {lastName} {Email}
            var usersForDropdown = await _dbContext.Users
                                                .OrderBy(u => u.FirstName)
                                                .ThenBy(u => u.LastName)
                                                .Select(u => new
                                                {
                                                    u.Id,
                                                    DisplayName = $"{u.FirstName} {u.LastName} ({u.Email})"
                                                })
                                                .ToListAsync();

            AvailableUsers = new SelectList(usersForDropdown, "Id", "DisplayName");

            // Fetch selected user's status and role if an ID is selected
            if (SelectedUserId.HasValue && SelectedUserId.Value > 0)
            {
                //.Include(u => u.UserSubjects)
                var user = await _dbContext.Users .Include(u => u.UserSubjects)
                                    .FirstOrDefaultAsync(u => u.Id == SelectedUserId.Value);
                if (user != null)
                {
                    SelectedUserStatus = user.Active ? "פעיל" : "לא פעיל";
                    string userRole = user.UserSubjects.FirstOrDefault()?.Role ?? "Unknown";

                    if (RoleDisplayNames.ContainsKey(userRole))
                    {
                        SelectedUserRole = RoleDisplayNames[userRole!];
                    }
                    else
                    {
                        SelectedUserRole = "תפקיד לא ידוע"; // Or some other appropriate default
                    }
                }
            }
            //DisplayRole(SelectedUserRole);

            IQueryable<UserLog> query = _dbContext.UsersLog
                                                .Include(ul => ul.User) // Eager load the affected user's details
                                                .OrderByDescending(ul => ul.Date); // Order by date, newest first

            if (SelectedUserId.HasValue && SelectedUserId.Value > 0)
            {
                query = query.Where(ul => ul.UserId == SelectedUserId.Value);
            }

            UsersLogs = await query.ToListAsync();

            // Populate InitiatorNames dictionary for the last column           
            var uniqueInitiatorIds = UsersLogs.Select(ul => ul.InitiatorId)
                                  .Where(id => id.HasValue && id.Value > 0)
                                  .Distinct()
                                  .ToList();
           
            var intInitiatorIds = uniqueInitiatorIds;

            if (intInitiatorIds.Any())
            {
                var initiatorUsers = await _dbContext.Users
                                                    .Where(u => intInitiatorIds.Contains(u.Id))
                                                    .ToListAsync();

                foreach (var user in initiatorUsers)
                {
                    InitiatorNames[user.Id.ToString()] = $"{user.FirstName} {user.LastName}";
                }
            }
        }
    }
}