using Bagrut_Eval.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System; // For DateTime

namespace Bagrut_Eval.Models
{
    public class Issue
    {
        [Key]
        public int Id { get; set; }

        // Foreign Key relationship to User (Initiator)
        [Required]
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; }

        [Required(ErrorMessage = "שדה התיאור נדרש למילוי")]
        [StringLength(500)] 
        public string? Description { get; set; }

        [Required]
        public DateTime OpenDate { get; set; }
        public DateTime? CloseDate { get; set; }

        [Required]
        public IssueStatus Status { get; set; } // Uses the enum from the same namespace
        public Export? Export { get; set; }

        // Foreign Key to the final selected Answer for this Issue
        // Nullable because an issue might be open or reopened without a final answer
        public int? FinalAnswerId { get; set; }
        [ForeignKey("FinalAnswerId")]
        public Answer? FinalAnswer { get; set; } // Navigation property to the selected final Answer

        [Required]
        public int ExamId { get; set; }
        [ForeignKey("ExamId")]
        public Exam? Exam { get; set; } // Navigation property

        // Foreign Key relationship to Exam question
        public int? PartId { get; set; }
        [ForeignKey("PartId")]
        public Part? Part { get; set; }
        public string? QuestionNumber { get; set; }

        public ICollection<Drawing> Drawings { get; set; } // Drawings associated with this Issue
        public ICollection<Answer> Answers { get; set; } // Discussion Answers for this Issue
        public ICollection<IssueLog> IssueLogs { get; set; } // Log entries for this Issue

        public Issue()
        {
            Drawings = new HashSet<Drawing>();
            Answers = new HashSet<Answer>();
            IssueLogs = new HashSet<IssueLog>();
            Status = IssueStatus.Open; 
        }
    }
}