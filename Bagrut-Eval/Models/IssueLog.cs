// Models/IssueLog.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bagrut_Eval.Models
{
    [Table("IssuesLog")]
    public class IssueLog
    {
        [Key]
        public int Id { get; set; }

        public int IssueId { get; set; }
        [ForeignKey("IssueId")]
        public Issue? Issue { get; set; } // Navigation property to the Issue

        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; } // Navigation property to the User (Senior/Admin) who made the change

        [Required]
        public DateTime LogDate { get; set; } // Using LogDate for clarity instead of just 'Date'

        [Required]
        [Column(TypeName = "longtext")] // Maps to LONGTEXT in MySQL
        public string? Description { get; set; }
    }
}
