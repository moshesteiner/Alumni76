using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Bagrut_Eval.Pages.Common;
using Bagrut_Eval.Utilities;
using DocumentFormat.OpenXml.Presentation;
using Markdig;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.IO;
using System.Security.Claims;

namespace Bagrut_Eval.Pages.Admin
{
    [Authorize(Roles = "Senior,Admin")]
    public class HistoryLogModel : BasePageModel<HistoryLogModel>
    {
        public class ReleaseNote
        {
            public string Version { get; set; } = string.Empty;
            public DateTime Date { get; set; }
            public List<string> Fixes { get; set; } = new();
            public List<string> Additions { get; set; } = new();
            public List<string> Changes { get; set; } = new();
            public List<string> Removes { get; set; } = new();
        }

        public string ReleaseNotesHtml { get; private set; } = string.Empty;
        public List<ReleaseNote> ReleaseNotes { get; private set; } = new();

        public HistoryLogModel(ApplicationDbContext dbContext, ILogger<HistoryLogModel> logger, ITimeProvider timeProvider)
                                    : base(dbContext, logger, timeProvider) { }

        public void OnGet()
        {
            base.OnGetAsync();
            // Path to your ChangeLog.md file (adjust as needed)
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "ChangeLog.md");

            if (!System.IO.File.Exists(filePath))
                return;

            var lines = System.IO.File.ReadAllLines(filePath);
            ReleaseNote? current = null;
            List<string>? currentList = null;

            foreach (var line in lines)
            {
                if (line.StartsWith("## ["))
                {
                    // Example: ## [1.2.0] - 2025-12-10
                    var parts = line.Replace("## [", "").Split(']');
                    var version = parts[0];
                    var rawDate = parts[1].Trim(); // " - 2025-12-13"
                    var datePart = rawDate.TrimStart('-', ' '); // "2025-12-13"                    
                    DateTime d = new DateTime();
                    DateTime.TryParseExact(datePart, "yyyy-MM-dd",
                                               System.Globalization.CultureInfo.InvariantCulture,
                                               System.Globalization.DateTimeStyles.None,
                                               out d);

                    current = new ReleaseNote
                    {
                        Version = version,
                        Date = d
                    };
                    ReleaseNotes.Add(current);
                }
                else if (line.StartsWith("###"))
                {
                    var changeType = line.Substring(4).Trim();
                    //currentList = changeType == "Added" ? current!.Additions : current!.Fixes;
                    switch(changeType)
                    {
                        case "Added":currentList = current!.Additions; break;
                        case "Fixed": currentList = current!.Fixes; break;
                        case "Changed": currentList = current!.Changes; break;
                        case "Removed": currentList = current!.Removes; break;
                    }
                }                
                else if (line.StartsWith("-") && currentList != null)
                {
                    currentList.Add(line.Substring(1).Trim());
                }
            }

            // Raw Tablw
            if (System.IO.File.Exists(filePath))
            {
                var markdown = System.IO.File.ReadAllText(filePath);

                // Convert Markdown to HTML using Markdig
                ReleaseNotesHtml = Markdown.ToHtml(markdown);
            }
            else
            {
                ReleaseNotesHtml = "<p><em>No changelog found.</em></p>";
            }
        }
    }
}

