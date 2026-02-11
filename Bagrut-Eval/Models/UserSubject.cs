using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bagrut_Eval.Models
{
    public class UserSubject
    {
        // Composite Primary Key configuration is typically done in DbContext (Next step)

        // Foreign Key to User
        public int UserId { get; set; }

        // Foreign Key to Subject
        public int SubjectId { get; set; }

        // Navigation properties
        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [ForeignKey(nameof(SubjectId))]
        public Subject? Subject { get; set; }

        [Required(ErrorMessage = "תפקיד נדרש")]
        [StringLength(32, ErrorMessage = "התפקיד לא יכול לחרוג מ-32 תווים")]
        public string? Role { get; set; }

        [Required] 
        public bool Active { get; set; } = true; 
    }
}