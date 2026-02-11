using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using System;
using System.Linq;
using System.Security.Claims; // Required for ClaimTypes.Email
using System.Threading.Tasks;

namespace Alumni76.Utilities
{
    public static class MaintenanceModeExtensions
    {
        private const string LoginPagePath = "/Index";

        public static IApplicationBuilder UseMaintenanceMode(this IApplicationBuilder app)
        {
            var configuration = app.ApplicationServices.GetRequiredService<IConfiguration>();
            var isMaintenanceModeEnabled = configuration.GetValue<bool>("MaintenanceMode:IsEnabled");
            string? MaintenancePath = configuration.GetValue<string>("MaintenanceMode:MaintenancePagePath");

            // Read the list of bypass users (same as before)
            var bypassUsers = configuration.GetSection("MaintenanceMode:BypassUsers").Get<string[]>() ?? Array.Empty<string>();

            if (isMaintenanceModeEnabled)
            {
                app.Use(async (context, next) =>
                {
                    var path = context.Request.Path.ToString();

                    // Define all paths that should NOT be redirected                    
                    var isExemptPath =
                    path.Equals(MaintenancePath, StringComparison.OrdinalIgnoreCase) ||
                    path.Equals("/", StringComparison.OrdinalIgnoreCase) ||           // Exempt the root path (usually the first hit)
                    path.Equals(LoginPagePath, StringComparison.OrdinalIgnoreCase) ||  // Exempt the explicit login page path (/Index)
                    path.Equals("/About", StringComparison.OrdinalIgnoreCase) ||
                    path.StartsWith("/lib") || path.StartsWith("/css") ||
                    path.StartsWith("/js") || path.StartsWith("/images");

                    if (isExemptPath)
                    {
                        // Allow access to static files, Maintenance page, and Login page
                        await next();
                        return;
                    }

                    // --- CHECK AUTHENTICATION AND BYPASS STATUS ---

                    // 1. If the user is authenticated (logged in)
                    if (context.User.Identity?.IsAuthenticated == true)
                    {
                        var isAdminBypass = context.User.IsInRole("Admin");
                        var userEmail = context.User.FindFirst(ClaimTypes.Email)?.Value;
                        var isUserListBypass = userEmail != null && bypassUsers.Contains(userEmail, StringComparer.OrdinalIgnoreCase);

                        if (isUserListBypass)   //    ||  isAdminBypass 
                        {
                            // Logged-in Admin or Bypass User is allowed to proceed
                            await next();
                            return;
                        }

                        // Logged-in, but NOT a bypass user. They must be redirected.
                    }

                    // 2. If we reach here: 
                    //    - The path is NOT exempt (not /Login, not static file, etc.)
                    //    - AND the user is not authenticated OR they failed the bypass check.
                    //    -> Redirect them to the Maintenance page.

                    context.Response.Redirect(MaintenancePath!);
                    return;

                    // If you were using the old code and isAdminBypass was the only check, 
                    // the redirect was implicit here for non-Admin users. We make it explicit now.
                    // await next(); // This is removed because the redirect takes over.
                });
            }

            return app;
        }
    }
}