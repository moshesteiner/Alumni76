using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bagrut_Eval.Models
{
    public class Exam
    {
        [Key]
        public int Id { get; set; }
        [Required(ErrorMessage = "שם הבחינה נדרש")]
        [StringLength(200, ErrorMessage = "שם הבחינה לא יכול לחרוג מ-200 תווים")]
        public string? ExamTitle { get; set; } 
        
        [Required(ErrorMessage = "סטטוס פעיל נדרש")] // "Active status is required"
        public bool Active { get; set; } = true;

        // Navigation property for relationships (added based on Issue.cs)
        public ICollection<Part> Parts { get; set; } // Issues related to this exam item
        public ICollection<AllowedExam> AllowedExams { get; set; }  // Allowed Exams per user
        public ICollection<ExamLog> ExamLogs { get; set; }

        [Display(Name = "נעול לשינויים")]
        public bool IsLocked { get; set; } = false; // Default to unlocked
        public int SubjectId { get; set; }

        // Navigation property for the one-to-many relationship
        [ForeignKey(nameof(SubjectId))]
        public Subject? Subject { get; set; }


        public Exam() // Add a constructor
        {
            Parts = new HashSet<Part>();
            AllowedExams = new HashSet<AllowedExam>();
            ExamLogs = new HashSet<ExamLog>();
        }
    }
}