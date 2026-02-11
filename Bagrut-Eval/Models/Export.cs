using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System;

namespace Bagrut_Eval.Models
{
    public class Export
    {
        // Enforcing a one-to-one relationship by making IssueId the primary key
        // This means there can be only one Export record for each Issue.
        [Key]
        public int IssueId { get; set; }
        [ForeignKey("IssueId")]
        public Issue? Issue { get; set; } // Navigation property to the associated Issue

        // This property is now nullable as per your request
        [Column(TypeName = "longtext")]
        public string? Description { get; set; }

        // Score can be a descriptive string, initially null
        [StringLength(64)]
        public string? Score { get; set; }

        [Required]
        public int SeniorId { get; set; } // Senior who performed the export
        [ForeignKey("SeniorId")]
        public User? Senior { get; set; } // Navigation property to the Senior user

        [Required]
        public DateTime Date { get; set; } // Date of export

        // Simple boolean flag to indicate if it's ready for final export
        public bool Exported { get; set; }
    }
}