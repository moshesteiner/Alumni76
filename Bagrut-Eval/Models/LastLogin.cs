// Bagrut_Eval.Models/LastLogin.cs (NEW MODEL)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System;

namespace Bagrut_Eval.Models
{
    public class LastLogin
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int UserId { get; set; }
        [ForeignKey("UserId")]
        public User? User { get; set; } // Navigation property to the User

        [Required]
        public DateTime LoginDate { get; set; } // Renamed from 'Date' for clarity
    }
}