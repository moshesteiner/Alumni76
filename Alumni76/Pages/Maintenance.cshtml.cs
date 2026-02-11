using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Alumni76.Pages
{
    [AllowAnonymous]
    public class MaintenanceModel : PageModel
    {
        public void OnGet()
        {
            // nothing happens here
        }
    }
}
