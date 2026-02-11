// Pages/AddPart.cshtml.cs
using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Bagrut_Eval.Pages.Common;
using Bagrut_Eval.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OfficeOpenXml;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Bagrut_Eval.Pages
{
    [Authorize(Roles = "Senior, Admin")]
    public class AddPartModel : BasePageModel<AddPartModel>
    {
        public IList<Part>? Parts { get; set; }

        [BindProperty(SupportsGet = true)]
        public Part? NewPart { get; set; }

        [ValidateNever]
        public Exam? Exam { get; set; }
        public bool IsLocked { get; set; } = false;

        [BindProperty(SupportsGet = true)]
        public int? SelectedExamId { get; set; }

        public string? SelectedExamTitle { get; set; }

        [BindProperty]
        public IFormFile? BulkAddFile { get; set; }

        public AddPartModel(ApplicationDbContext dbContext, ILogger<AddPartModel> logger,
                          ITimeProvider timeProvider) : base(dbContext, logger, timeProvider)
        {
        }
        private async Task LoadDataAsync(int? selectedExamIdFilter = null)
        {
            AvailableExams = new SelectList(await _dbContext.Exams
                                            .Where(e => e.Active && e.SubjectId == SubjectId)
                                            .OrderBy(e => e.ExamTitle)
                                            .Select(e => new SelectListItem
                                            {
                                                Value = e.Id.ToString(),
                                                Text = $"{e.ExamTitle}{(e.IsLocked ? " - נעול" : "")}"
                                            })
                                            .ToListAsync(), "Value", "Text");
           

            IQueryable<Part> partsQuery = _dbContext.Parts.Include(p => p.Exam);    

            if (selectedExamIdFilter.HasValue && selectedExamIdFilter.Value > 0)
            {
                partsQuery = partsQuery.Where(p => p.ExamId == selectedExamIdFilter.Value);
                SelectedExamTitle = (await _dbContext.Exams.FindAsync(selectedExamIdFilter.Value))?.ExamTitle;
                var exam = await _dbContext.Exams .Where(e => e.Id == selectedExamIdFilter).FirstOrDefaultAsync();
                IsLocked = exam != null ? exam.IsLocked : false;
            }
            else
            {
                partsQuery = partsQuery.Where(p => false);
                SelectedExamTitle = null;
            }

            var partsList = await partsQuery.ToListAsync();

            Parts = partsList
                .OrderBy(p =>
                {
                    // Find the first part of the string that is a number
                    var firstPart = p.QuestionNumber!.Split('-').FirstOrDefault();

                    // Try to parse it. If it fails, use a high number to send it to the end.
                    if (int.TryParse(firstPart, out int num))
                    {
                        return num;
                    }
                    return int.MaxValue;
                })
                .ThenBy(p => p.QuestionPart)
                .ToList();
        }

        public new async Task<IActionResult> OnGetAsync()
        {
            ModelState.Clear();
            await base.OnGetAsync();
            LoadAdminContext();
            if (IsSpecialAdmin)
                return RedirectToPage("/Index");   // avoid Special Admin this page - as SubjectId is not set
            await LoadDataAsync(SelectedExamId);

            NewPart = new Part();
            if (SelectedExamId.HasValue && SelectedExamId.Value > 0)
            {
                NewPart.ExamId = SelectedExamId.Value;
            }
            return Page();
        }

        public async Task<IActionResult> OnGetEditAsync(int id, int selectedExamId)
        {
            _logger.LogInformation($"OnGetEditAsync called for Part ID: {id}, SelectedExamId: {selectedExamId}");

            NewPart = await _dbContext.Parts.FindAsync(id);

            if (NewPart == null)
            {
                TempData["ErrorMessage"] = "סעיף הבחינה לא נמצא לעריכה.";
                _logger.LogWarning($"Part with ID {id} not found for editing.");
                return RedirectToPage("/AddPart", new { SelectedExamId = selectedExamId });
            }

            SelectedExamId = selectedExamId;
            _logger.LogInformation($"Successfully loaded part {id} for editing. SelectedExamId set to {selectedExamId}.");
            await LoadDataAsync(selectedExamId);
            return Page();
        }

        public new async Task<IActionResult> OnPostAsync()
        {
            await base.OnGetAsync();

            if (NewPart!.Id == 0 && SelectedExamId.HasValue)
            {
                NewPart.ExamId = SelectedExamId.Value;
                _logger.LogInformation($"New part being added with ExamId: {NewPart.ExamId}");
            }
            else
            {
                _logger.LogInformation($"Updating existing part or no SelectedExamId. Part ID: {NewPart.Id}, ExamId: {NewPart.ExamId}");
            }

            ModelState.Remove("NewPart.Exam");

            if (!ModelState.IsValid)
            {
                _logger.LogWarning("AddPart form submission failed due to validation errors.");
                TempData["ErrorMessage"] = "שגיאת קלט: אנא בדוק את הפרטים המודגשים באדום.";
                await LoadDataAsync(SelectedExamId);
                return Page();
            }

            var exam = await _dbContext.Exams.FindAsync(NewPart.ExamId);
            if (exam == null)
            {
                _logger.LogError($"Attempted to save part with non-existent ExamId: {NewPart.ExamId}");
                TempData["ErrorMessage"] = "שגיאה: הבחינה שנבחרה אינה קיימת במערכת.";
                await LoadDataAsync(SelectedExamId);
                return Page();
            }

            NewPart.Exam = exam;

            if (NewPart.Id == 0)
            {
                _dbContext.Parts.Add(NewPart);
                _logger.LogInformation($"Attempting to add new part for ExamId: {NewPart.ExamId}");
                TempData["SuccessMessage"] = "סעיף הבחינה נוסף בהצלחה!";
            }
            else
            {
                _dbContext.Parts.Update(NewPart);
                _logger.LogInformation($"Attempting to update part ID: {NewPart.Id} for ExamId: {NewPart.ExamId}");
                TempData["SuccessMessage"] = "סעיף הבחינה עודכן בהצלחה!";
            }

            try
            {
                await _dbContext.SaveChangesAsync();
                _logger.LogInformation("Database changes saved successfully.");
                return RedirectToPage("/AddPart", new { SelectedExamId = NewPart.ExamId });
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, "Error saving changes to database for part.");
                TempData["ErrorMessage"] = "שגיאה בשמירת נתונים: " + ex.Message;
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Inner exception details.");
                }
                await LoadDataAsync(SelectedExamId);
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int id, int selectedExamId)
        {
            _logger.LogInformation($"OnPostDeleteAsync called for Part ID: {id}.");

            var hasIssues = await _dbContext.Issues.AnyAsync(i => i.PartId == id);

            if (hasIssues)
            {
                TempData["ErrorMessage"] = "לא ניתן למחוק סעיף בחינה שיש לו תקלות מקושרות. יש לטפל קודם בתקלות אלה.";
                _logger.LogWarning($"Deletion of Part ID {id} failed because it has associated issues.");
                return RedirectToPage("/AddPart", new { SelectedExamId = selectedExamId });
            }

            var partToDelete = await _dbContext.Parts.FindAsync(id);

            if (partToDelete == null)
            {
                TempData["ErrorMessage"] = "סעיף הבחינה לא נמצא למחיקה.";
                _logger.LogWarning($"Part with ID {id} not found for deletion.");
                return RedirectToPage("/AddPart", new { SelectedExamId = selectedExamId });
            }

            _dbContext.Parts.Remove(partToDelete);

            try
            {
                await _dbContext.SaveChangesAsync();
                TempData["SuccessMessage"] = "הסעיף נמחק בהצלחה.";
                _logger.LogInformation($"Successfully deleted part with ID: {id}.");
            }
            catch (DbUpdateException ex)
            {
                _logger.LogError(ex, $"Error deleting part with ID {id}.");
                TempData["ErrorMessage"] = "שגיאה במחיקת הסעיף: " + ex.Message;
                if (ex.InnerException != null)
                {
                    _logger.LogError(ex.InnerException, "Inner exception details.");
                }
            }

            return RedirectToPage("/AddPart", new { SelectedExamId = selectedExamId });
        }

        public async Task<IActionResult> OnPostBulkAddAsync()
        {
            if (!SelectedExamId.HasValue || SelectedExamId.Value <= 0)
            {
                TempData["ErrorMessage"] = "אנא בחר בחינה לפני ביצוע טעינה מקובץ.";
                await LoadDataAsync();
                return Page();
            }

            if (BulkAddFile == null || BulkAddFile.Length == 0)
            {
                TempData["ErrorMessage"] = "נא לבחור קובץ חוקי.";
                await LoadDataAsync(SelectedExamId);
                return Page();
            }

            try
            {
                ExcelPackage.License.SetNonCommercialPersonal("Moshe Steiner");

                var newParts = new List<Part>();
                int addedCount = 0;
                int updatedCount = 0;

                using (var stream = new MemoryStream())
                {
                    await BulkAddFile.CopyToAsync(stream);
                    using (var package = new ExcelPackage(stream))
                    {
                        var worksheet = package.Workbook.Worksheets.FirstOrDefault();
                        if (worksheet == null)
                        {
                            TempData["ErrorMessage"] = "שגיאה: הקובץ לא מכיל גיליונות עבודה.";
                            await LoadDataAsync(SelectedExamId);
                            return Page();
                        }

                        var rowCount = worksheet.Dimension.Rows;

                        for (int row = 2; row <= rowCount; row++)
                        {
                            string questionNumber = worksheet.Cells[row, 1].Text;
                            string questionPart = worksheet.Cells[row, 2].Text;
                            string score = worksheet.Cells[row, 3].Text;

                            if (!string.IsNullOrWhiteSpace(questionNumber) &&
                                !string.IsNullOrWhiteSpace(score))
                            {
                                var existingPart = await _dbContext.Parts
                                    .Where(p => p.ExamId == SelectedExamId.Value &&
                                                p.QuestionNumber == questionNumber &&
                                                p.QuestionPart == questionPart)
                                    .FirstOrDefaultAsync();

                                if (existingPart != null)
                                {
                                    existingPart.Score = score;
                                    _dbContext.Parts.Update(existingPart);
                                    updatedCount++;
                                }
                                else
                                {
                                    newParts.Add(new Part
                                    {
                                        ExamId = SelectedExamId.Value,
                                        QuestionNumber = questionNumber,
                                        QuestionPart = questionPart,
                                        Score = score
                                    });
                                    addedCount++;
                                }
                            }
                        }
                    }
                }

                if (newParts.Any())
                {
                    await _dbContext.Parts.AddRangeAsync(newParts);
                }

                await _dbContext.SaveChangesAsync();

                string successMessage = $"טעינת קובץ הסתיימה בהצלחה. נוספו {addedCount} סעיפים ו- {updatedCount} עודכנו.";
                TempData["SuccessMessage"] = successMessage;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during bulk add from file.");
                TempData["ErrorMessage"] = $"שגיאה בטעינת הקובץ: {ex.Message}";
            }

            await LoadDataAsync(SelectedExamId);
            return RedirectToPage("/AddPart", new { SelectedExamId = SelectedExamId });
        }
    }
}