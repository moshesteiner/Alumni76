// Bagrut_Eval.Models/Drawing.cs (NEW MODEL)
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Bagrut_Eval.Models
{
    public class Drawing
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int IssueId { get; set; } // Foreign key to Issue
        [ForeignKey("IssueId")]
        public Issue? Issue { get; set; } // Navigation property to the associated Issue

        [Required]
        [StringLength(500)] // Sufficient length for file path or URL
        // Store the file path or URL, not the binary content itself
        public string? FilePathOrUrl { get; set; } // Renamed from 'Drawing' for clarity

        // Optional: you might want to store original file name, MIME type, upload date etc.
        [StringLength(255)]
        public string? OriginalFileName { get; set; }

        [StringLength(50)]
        public string? MimeType { get; set; } // e.g., "image/png", "application/pdf"

        public DateTime UploadDate { get; set; } = DateTime.UtcNow; // Using UtcNow for consistency
    }
}