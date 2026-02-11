// File: Models/MetricLog.cs (or wherever you keep your EF Core models)

using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bagrut_Eval.Models 
{
    [Table("MetricsLog")] // Explicitly set the table name in MySQL
    public class MetricLog
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        // Foreign key to the Metric table, linking this log entry to a specific metric
        public int MetricId { get; set; }

        // The ID of the user who performed the action (admin)
        public int UserId { get; set; }

        // The date and time the log entry was created
        public DateTime Date { get; set; }

        // Describes the action performed (e.g., "Created", "Deleted", "Modified")
        [Required]
        [StringLength(50)] // Recommended length for action descriptions
        public string? Action { get; set; } // This will now hold "Created", "Deleted", etc.

        // --- Snapshot of the Metric data at the time of the log entry ---
        public int ExamId { get; set; } // Copy from Metric for the snapshot
        public string? QuestionNumber { get; set; }
        public string? Part { get; set; }
        public string? RuleDescription { get; set; }
        public int? Score { get; set; }
        public string? ScoreType { get; set; }
        public string? Status { get; set; } // The status of the Metric *at the time this log entry was created*
    }
}