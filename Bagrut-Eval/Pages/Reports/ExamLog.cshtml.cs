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

namespace Bagrut_Eval.Pages.Reports
{
    [Authorize(Roles = "Admin")]
    public class ExamLogModel : PageModel // Changed class name
    {
        private readonly ApplicationDbContext _context;       

        [BindProperty(SupportsGet = true)]
        public int? SelectedExamId { get; set; }

        public SelectList? AvailableExams { get; set; }

        public List<ExamLog> ExamsLogs { get; set; } = new List<ExamLog>();

        public ExamLogModel(ApplicationDbContext context) // Changed constructor name
        {
            _context = context;
        }
        public async Task OnGetAsync(int? selectedExamId)
        {
            await LoadReportDataAsync(selectedExamId);
        }

        public async Task<IActionResult> OnPostFilterAsync()
        {
            await LoadReportDataAsync(SelectedExamId);
            return Page();
        }

        private async Task LoadReportDataAsync(int? selectedExamId)
        {
            SelectedExamId = selectedExamId;

            AvailableExams = new SelectList(await _context.Exams
                                                         .Where(e => e.Active)
                                                         .OrderBy(e => e.ExamTitle)
                                                         .ToListAsync(), "Id", "ExamTitle");

            IQueryable<ExamLog> query = _context.ExamsLog // Ensure this is correct DbSet name
                                                 .Include(el => el.Exam)
                                                 .Include(el => el.User);

            if (SelectedExamId.HasValue && SelectedExamId.Value > 0)
            {
                query = query.Where(el => el.ExamId == SelectedExamId.Value);
            }

            ExamsLogs = await query
                                 .OrderBy(el => el.Exam!.ExamTitle)
                                 .ThenByDescending(el => el.Date)
                                 .ToListAsync();
        }
    }
}