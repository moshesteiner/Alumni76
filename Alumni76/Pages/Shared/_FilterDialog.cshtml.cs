using Alumni76.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.Text.Json;

namespace Alumni76.Pages.Shared
{
    public class _FilterDialogModel : PageModel
    {
        public const string FilterSessionKey = "FilterState";
        [BindProperty]  //(SupportsGet = true)]
        public FilterModel? FilterModel { get; set; }
        public void OnGet()
        {
        }        
    }
}
