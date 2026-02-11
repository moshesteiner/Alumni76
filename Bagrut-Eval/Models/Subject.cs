using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bagrut_Eval.Models
{
    public class Subject
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        [Display(Name = "שם המקצוע")]
        public string Title { get; set; } = string.Empty;
        [Required]
        public bool Active { get; set; } = true;

        // Navigation property for the one-to-many relationship (Subject -> Exams)
        public ICollection<Exam> Exams { get; set; } = new List<Exam>();

        // Navigation property for the many-to-many relationship (Subject <-> Users)
        public ICollection<UserSubject> UserSubjects { get; set; } = new List<UserSubject>();
    }
}