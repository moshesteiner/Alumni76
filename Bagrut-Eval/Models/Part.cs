using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bagrut_Eval.Models
{
    public class Part
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int ExamId { get; set; } // Foreign key to Exam

        [ForeignKey("ExamId")]
        public Exam? Exam { get; set; } // Navigation property to Exam        
        
        [Required]
        public string? QuestionNumber { get; set; } 
        public string? QuestionPart { get; set; } 

        [StringLength(8)]
        public string? Score { get; set; } // Potentially rename to ItemNumber or SectionItem for clarity

        // Navigation property for relationships (added based on Issue.cs)
        public ICollection<Issue> Issues { get; set; } // Issues related to this exam item

        public Part()
        {
            Issues = new HashSet<Issue>(); // Initialize the collection to avoid null reference exceptions
        }
    }
}
