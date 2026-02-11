using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bagrut_Eval.Pages.Reports
{
    [Authorize(Roles = "Admin")] // Only Admins can access the reports section
    public class IndexModel : PageModel
    {
        public void OnGet()
        {
            // This page will primarily serve as a container for the reports menu.
            // No specific data loading is needed for this landing page itself.
        }
    }
}