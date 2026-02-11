using Alumni76.Data;
using Alumni76.Pages.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;


namespace Alumni76.Pages
{
    public class AboutModel : BasePageModel<AboutModel>
    {
         public AboutModel(ApplicationDbContext dbContext, ILogger<AboutModel> logger, ITimeProvider timeProvider)
                                    : base(dbContext, logger, timeProvider) { }

        public void OnGet()
        {
            base.OnGetAsync();
        }
    }

}
