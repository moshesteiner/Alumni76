using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Bagrut_Eval.Pages
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
