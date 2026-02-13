using Alumni76.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Alumni76.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }
        [Required(ErrorMessage = "חובה להזין שם"), StringLength(50)]
        public string FirstName { get; set; } = "";
        [Required(ErrorMessage = "חובה להזין שם משפחה"), StringLength(50)]
        public string LastName { get; set; } = "";
        public string? MaidenName { get; set; } // שם נעורים
        public string? NickName { get; set; }
        //[Required(ErrorMessage = "חובה להזין כיתה"), StringLength(8)]
        [StringLength(8)]
        public string? Class { get; set; } = "";
        [Required(ErrorMessage = "חובה להזין אימייל"), EmailAddress]
        private string _email = string.Empty;
        public string Email
        {
            get => _email;
            set => _email = (value ?? string.Empty).Trim().ToLower();
        }
        public string? PasswordHash { get; set; } = "";

        [Required(ErrorMessage = "חובה להזין מספר טלפון")]
        [RegularExpression(@"^(\+?\d{1,3}[- ]?)?\(?(\d{3})\)?[- ]?(\d{3,4})[- ]?(\d{4})$|^0?(\d{2,3})[\s-]?(\d{3})[\s-]?(\d{4})$", ErrorMessage = "פורמט לא תקין")]
        public string? Phone1 { get; set; }

        // No [Required] here, but the same Regex if they choose to type something
        [RegularExpression(@"^(\+?\d{1,3}[- ]?)?\(?(\d{3})\)?[- ]?(\d{3,4})[- ]?(\d{4})$|^0?(\d{2,3})[\s-]?(\d{3})[\s-]?(\d{4})$", ErrorMessage = "פורמט לא תקין")]
        public string? Phone2 { get; set; }

        public string? Address { get; set; }
        public bool IsAdmin { get; set; } = false;
        [Required]
        public bool Active { get; set; } = true;
        public string? TwoFactorCode { get; set; }
        public DateTime? TwoFactorCodeExpiration { get; set; }
        public DateTime? LastLogin { get; set; }   // Tracks if it's the first time
        public bool EmailVerified { get; set; }     // Set to false if email changes
        public string? PendingEmail { get; set; }
    }
}