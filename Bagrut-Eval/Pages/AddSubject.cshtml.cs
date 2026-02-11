using Bagrut_Eval.Data;
using Bagrut_Eval.Models;
using Bagrut_Eval.Pages.Common; // Assuming your BasePageModel is here
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

// Assuming 'SpecialAdmin' is a role you can check for, or you use your CheckForSpecialAdmin logic
[Authorize(Roles = "Admin")] // Use the base role if SpecialAdmin is an internal check
public class AddSubjectModel : BasePageModel<AddSubjectModel>
{
    // --- View Model for Listing Subjects ---
    public List<SubjectDisplayItem> Subjects { get; set; } = new List<SubjectDisplayItem>();

    // --- BindProperty for Adding New Subject ---
    [BindProperty]
    public NewSubjectInputModel NewSubject { get; set; } = new NewSubjectInputModel();

    // --- Inner Models ---

    public class SubjectDisplayItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public bool Active { get; set; }
    }

    public class NewSubjectInputModel
    {
        [Required(ErrorMessage = "שם מקצוע נדרש")]
        [StringLength(100, ErrorMessage = "שם מקצוע לא יכול לחרוג מ-100 תווים")]
        [Display(Name = "שם מקצוע חדש")]
        public string Title { get; set; } = string.Empty;
    }

    // --- Constructor and OnGet ---

    public AddSubjectModel(ApplicationDbContext dbContext, ILogger<AddSubjectModel> logger, ITimeProvider timeprovider)
                    : base(dbContext, logger, timeprovider)
    {
    }

    public new async Task OnGetAsync()
    {
        await base.OnGetAsync();
        CheckForSpecialAdmin();
        if (!IsSpecialAdmin)
        {
            // Redirect unauthorized users (e.g., non-special admins) to a forbidden page
            RedirectToPage("/AccessDenied");
            return;
        }

        await LoadSubjectsAsync();
    }

    private async Task LoadSubjectsAsync()
    {
        Subjects = await _dbContext.Subjects
            .OrderBy(s => s.Id)
            .Select(s => new SubjectDisplayItem
            {
                Id = s.Id,
                Title = s.Title,
                Active = s.Active
            })
            .ToListAsync();
    }

    // --- Handler for Adding New Subject ---

    public async Task<IActionResult> OnPostAddSubjectAsync()
    {
        CheckForSpecialAdmin();
        if (!IsSpecialAdmin) return Forbid();

        // Check model state (e.g., required/string length)
        if (!ModelState.IsValid)
        {
            await LoadSubjectsAsync();
            return Page();
        }

        // Check for duplicate title (case-insensitive)
        if (await _dbContext.Subjects.AnyAsync(s => s.Title.ToLower() == NewSubject.Title.ToLower()))
        {
            ModelState.AddModelError("NewSubject.Title", "מקצוע בשם זה כבר קיים.");
            await LoadSubjectsAsync();
            return Page();
        }

        var newSubject = new Subject { Title = NewSubject.Title };
        _dbContext.Subjects.Add(newSubject);

        await _dbContext.SaveChangesAsync();

        TempData["SuccessMessage"] = $"המקצוע **{newSubject.Title}** נוסף בהצלחה.";
        return RedirectToPage(); // Redirect to clear form and show update
    }

    // --- Handler for Updating/Renaming Subject ---

    public async Task<IActionResult> OnPostUpdateSubjectAsync(int id, string title)
    {
        CheckForSpecialAdmin();
        if (!IsSpecialAdmin) return Forbid();

        if (string.IsNullOrWhiteSpace(title) || title.Length > 100)
        {
            return new JsonResult(new { success = false, message = "שם מקצוע לא חוקי" }) { StatusCode = 400 };
        }

        var subjectToUpdate = await _dbContext.Subjects.FindAsync(id);

        if (subjectToUpdate == null)
        {
            return new JsonResult(new { success = false, message = "מקצוע לא נמצא" }) { StatusCode = 404 };
        }

        // Check for duplicate title (excluding the subject being updated)
        if (await _dbContext.Subjects.AnyAsync(s => s.Id != id && s.Title.ToLower() == title.ToLower()))
        {
            return new JsonResult(new { success = false, message = "מקצוע בשם זה כבר קיים" }) { StatusCode = 409 };
        }

        subjectToUpdate.Title = title;
        await _dbContext.SaveChangesAsync();

        return new JsonResult(new { success = true, message = $"שם המקצוע עודכן ל-{title}." });
    }

    public IActionResult OnGetSetTempDataError(string message)
    {
        if (!string.IsNullOrEmpty(message))
        {
            // This is the CRITICAL line that makes the message available to Razor
            TempData["ErrorMessage"] = message;
        }
        return Page();
    }
    public async Task<IActionResult> OnPostToggleActiveStatus(int id)
    {
        // 1. Get the subject
        var subject = await _dbContext.Subjects.FindAsync(id);

        if (subject == null)
        {
            return new NotFoundObjectResult(new { success = false, message = "הנושא לא נמצא." });
        }

        // 2. Toggle the Active status
        subject.Active = !subject.Active;

        try
        {
            // 3. Save the change
            await _dbContext.SaveChangesAsync();

            // 4. Return success response with the new status
            return new JsonResult(new
            {
                success = true,
                newActiveStatus = subject.Active,
                message = $"המקצוע {subject.Title} עודכן ל: {(subject.Active ? "פעיל" : "לא פעיל")}"
            });
        }
        catch (Exception ex)
        {
            // 5. Handle and log database errors
            _logger.LogError(ex, "Error toggling subject active status for ID {SubjectId}", id);
            return new BadRequestObjectResult(new { success = false, message = "שגיאה בשמירה לבסיס הנתונים." });
        }
    }
}