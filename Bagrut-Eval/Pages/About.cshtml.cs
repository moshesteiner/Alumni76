using Bagrut_Eval.Data;
using Bagrut_Eval.Pages.Admin;
using Bagrut_Eval.Pages.Common;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;


namespace Bagrut_Eval.Pages
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
