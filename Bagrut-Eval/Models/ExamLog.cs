// Models/ExamLog.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bagrut_Eval.Models
{
    [Table("ExamsLog")] 
    public class ExamLog
    {
        [Key]
        public int Id { get; set; }

        public int ExamId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        public int UserId { get; set; }

        [Required]
        [StringLength(500)] // Adjust length as needed for descriptions
        public string? Description { get; set; }

        // Navigation properties
        [ForeignKey("ExamId")]
        public Exam? Exam { get; set; }

        [ForeignKey("UserId")]
        public User? User { get; set; }
    }
}