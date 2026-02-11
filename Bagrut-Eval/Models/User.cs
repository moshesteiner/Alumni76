using Bagrut_Eval.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Bagrut_Eval.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "שם פרטי נדרש")]
        [StringLength(100, ErrorMessage = "שם פרטי לא יכול לחרוג מ-100 תווים")]
        public string? FirstName { get; set; }

        [Required(ErrorMessage = "שם משפחה נדרש")]
        [StringLength(100, ErrorMessage = "שם משפחה לא יכול לחרוג מ-100 תווים")]
        public string? LastName { get; set; }

        [Required(ErrorMessage = "כתובת אימייל נדרשת")]
        [EmailAddress(ErrorMessage = "פורמט אימייל לא תקין")]
        [StringLength(255, ErrorMessage = "כתובת אימייל לא יכולה לחרוג מ-255 תווים")]
        public string? Email { get; set; }


        [Required(ErrorMessage = "טלפון נדרש")]
        // 9-10 digits, allowing optional hyphens/spaces for readability (e.g., 05X-XXXXXXX or 03XXXXXXX)
        // ^(0(?:2|3|4|8|9|5[0-9]|7[2-9]|800)-?\d{7}|1-?\d{7})$
        // A simpler, more forgiving regex for basic 9-10 digit Israeli mobile/landline number:
        [RegularExpression(@"^0?(\d{2,3})[\s-]?(\d{7})$", ErrorMessage = "פורמט טלפון לא חוקי. (דוגמה: 05X-XXXXXXX)")]
        [StringLength(15, ErrorMessage = "מספר הטלפון ארוך מדי")] // Max length allowing formatting characters (hyphens/spaces)
        public string Phone { get; set; } = string.Empty;

        /*
        <p>
            מספר טלפון:
            **+972-@(Model.User.Phone.StartsWith("0") ? Model.User.Phone.Substring(1) : Model.User.Phone)**
        </p>
        */

        [BindNever] 
        public string PasswordHash { get; set; }        

        [Required]
        public bool Active { get; set; } = true;
        public string? TwoFactorCode { get; set; }
        public DateTime? TwoFactorCodeExpiration { get; set; }

        // Navigation properties for relationships
        [BindNever]
        public ICollection<Issue> CreatedIssues { get; set; } // Issues opened by this user
        [BindNever]
        public ICollection<AllowedExam> AllowedExams { get; set; } // Exams this user is allowed to evaluate
        [BindNever]
        public ICollection<ExamLog> ExamLogs { get; set; } // Logs related to exams, where this user is the actor
        [BindNever]
        public ICollection<IssueLog> CreatedIssueLogs { get; set; } // Issue logs created by this user
        [BindNever]
        public ICollection<UserLog> UserLogsAsSubject { get; set; } // User logs where this user is the subject of the log
        [BindNever]
        public ICollection<UserLog> CreatedUserLogs { get; set; } // User logs created by this user (as initiator)
        [BindNever]
        public ICollection<Answer> AnswersGiven { get; set; } // Answers provided by this user (as Senior)
        [BindNever]
        public ICollection<Export> ExportsPerformed { get; set; } // Exports performed by this user (as Senior)
        [BindNever]
        public ICollection<LastLogin> LoginRecords { get; set; } // Login records for this user
        public ICollection<UserSubject> UserSubjects { get; set; }  // for many to many relationship

        public User() // Add a constructor
        {
            CreatedIssues = new HashSet<Issue>();
            AllowedExams = new HashSet<AllowedExam>();
            ExamLogs = new HashSet<ExamLog>();
            CreatedIssueLogs = new HashSet<IssueLog>();
            UserLogsAsSubject = new HashSet<UserLog>();
            CreatedUserLogs = new HashSet<UserLog>();
            AnswersGiven = new HashSet<Answer>();
            ExportsPerformed = new HashSet<Export>();
            LoginRecords = new HashSet<LastLogin>();
            PasswordHash = "pass"; // Default for development/testing
            UserSubjects = new List<UserSubject>();
        }
    }
}