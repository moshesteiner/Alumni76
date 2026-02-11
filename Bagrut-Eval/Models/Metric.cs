// File: Models/Metric.cs (or wherever you keep your EF Core models)

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bagrut_Eval.Models 
{
    [Table("metrics")] // Explicitly set the table name in MySQL
    public class Metric
    {
        [Key] //  Primary Key
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // Configures it for auto-increment
        public int Id { get; set; }
      
        public int ExamId { get; set; }

        [ForeignKey("ExamId")] // Explicitly links this to the ExamId property
        public Exam? Exam { get; set; } 

        public string? QuestionNumber { get; set; }

        // Nullable as some general rules might not relate to a specific part
        public string? Part { get; set; }

        [Required] 
        public string? RuleDescription { get; set; }
        public int? Score { get; set; }

        [Required] 
        // Possible values: "Score", "Penalty", "GeneralPenalty", "Alternative Solution", "Question Title"
        public string? ScoreType { get; set; }

        // Tracks admin modifications: null (original/unchanged), "new", "deleted", "modified"
        public string? Status { get; set; }
    }
}