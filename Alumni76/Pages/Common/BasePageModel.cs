using Alumni76.Data;
using Alumni76.Models;
using Alumni76.Utilities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Alumni76.Pages.Common
{
    public abstract class BasePageModel<T> : PageModel
    {
        protected readonly ApplicationDbContext _dbContext;
        protected readonly ILogger<T> _logger;
        protected readonly ITimeProvider _timeProvider;

        protected const string FilterSessionKey = "UserFilterSettings";
        protected const string specialAdminId = "special_admin_user_id";
        protected const string specialAdminEmail = "steiner.moshe@gmail.com";

        public bool IsSpecialAdmin { get; set; }

        [TempData]
        public string? LoggedInUserName { get; set; }

        protected int CurrentUserId = 0;
        public bool IsAdmin { get; set; } = false;

        public BasePageModel(ApplicationDbContext dbContext, ILogger<T> logger, ITimeProvider timeProvider)
        {
            _dbContext = dbContext;
            _logger = logger;
            _timeProvider = timeProvider;
        }

        // Simplified Roles
        public Dictionary<string, string> RoleDisplayNames { get; set; } = new Dictionary<string, string>
        {
            { "Admin", "מנהל מערכת" },
            { "Member", "חבר/ה" }
        };


        public void CheckForSpecialAdmin()
        {
            // Checks if the logged-in user's ID matches our hardcoded back-door ID
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            IsSpecialAdmin = (userId == specialAdminId);
        }

        protected virtual void LoadUserContext()
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                // Get ID
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier);
                if (userIdClaim != null) int.TryParse(userIdClaim.Value, out CurrentUserId);

                // Get Role
                var role = User.FindFirst(ClaimTypes.Role)?.Value;
                IsAdmin = role == "Admin";                
            }
        }

        protected virtual Task OnGetAsync()
        {
            string? referer = Request.Headers["Referer"].ToString();
            string currentPath = Request.Path.ToString();

            // Check if the user came from a DIFFERENT page, and clear the FilterSessionKey
            if (!string.IsNullOrEmpty(referer) && !referer.Contains(currentPath))
            {
                HttpContext.Session.Remove(FilterSessionKey);
            }


            LoadUserContext();
            return Task.CompletedTask;
        }

        protected virtual Task OnPostAsync()
        {
            LoadUserContext();
            return Task.CompletedTask;
        }       
    }
}