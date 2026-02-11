// Bagrut_Eval.Models/Answer.cs (NEW MODEL)
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bagrut_Eval.Models
{
    public class Answer
    {
        [Key]
        public int Id { get; set; }

        public int IssueId { get; set; } // Foreign key to Issue (for discussion answers)
        [ForeignKey("IssueId")]
        public Issue? Issue { get; set; } // Navigation property to the associated Issue

        [Required(ErrorMessage="חובה להקליד תשובה")]
        [StringLength(1000)] // For potentially long answer content
        public string? Content { get; set; } // Renamed from 'Answer' for clarity (to avoid conflict with table name)

        [Required(ErrorMessage ="חובה להזין את כמות ההורדה. ניתן להוסיף טקסט")]
        // As per your clarification, Score can be a descriptive string (e.g., "3% max 10%")
        [StringLength(64)] // Adjust length as needed for descriptive score
        public string? Score { get; set; }

        public int SeniorId { get; set; } // Senior who provided/approved this answer
        [ForeignKey("SeniorId")]
        public User? Senior { get; set; } // Navigation property to the Senior user

        [Required]
        public DateTime Date { get; set; } // Date the answer was provided
        [BindNever] // Collections are typically not bound from forms directly
        public ICollection<Export> Exports { get; set; }

        public Answer()
        {
            // Initialize the new collection property
            Exports = new HashSet<Export>();

        }
    }
}