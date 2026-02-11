using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bagrut_Eval.Models
{
    // Define the composite primary key using data annotations
    // This requires specific configuration in your DbContext's OnModelCreating method
    // to correctly define the composite key.
    public class AllowedExam
    {
        [Required] // Though defined as NOT NULL in DB, good practice to include
        public int UserId { get; set; }

        [Required] // Though defined as NOT NULL in DB, good practice to include
        public int ExamId { get; set; }

        // Navigation properties to represent the relationships
        // These are crucial for EF Core to understand the links
        [ForeignKey("UserId")]
        public User? User { get; set; }

        [ForeignKey("ExamId")]
        public Exam? Exam { get; set; }
    }
}
