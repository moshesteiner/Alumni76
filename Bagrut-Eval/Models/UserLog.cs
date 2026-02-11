using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bagrut_Eval.Models // Adjust this namespace to match your project's structure
{
    [Table("UsersLog")]
    public class UserLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; } // The ID of the user whose data was affected

        [ForeignKey("UserId")]
        public User? User { get; set; } // Navigation property to the affected User

        public int? InitiatorId { get; set; } // The ID of the admin who made the change (nullable)
        [ForeignKey("InitiatorId")]
        public User? Initiator { get; set; } // Navigation property to the admin who made the change

        [Required]
        public DateTime Date { get; set; }

        [Required]
        [StringLength(1000)] // Adjust length as needed, 1000 characters should be sufficient
        public string? Description { get; set; }
    }
}