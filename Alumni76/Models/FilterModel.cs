// File: Models/FilterSortModel.cs
using System;
using System.ComponentModel.DataAnnotations;

namespace Alumni76.Models
{
    public class FilterModel
    {
        // --- Filter Properties ---

        [Display(Name = "הצג פריטים סגורים")]
        public bool ShowClosed { get; set; } = true;
        public bool DisplayShowClosed { get; set; } = true;   // show in GUI

        [Display(Name = "הצג פריטים חדשים מאז כניסתי האחרונה")]
        public bool ShowNewerThanLastLogin { get; set; } = false;
        public bool DisplayShowNewerThanLastLogin { get; set; } = false; // show in GUI

        [Display(Name = "חיפוש לפי טקסט")]
        public string? DescriptionSearch { get; set; }
        public bool DisplayDescriptionSearch { get; set; } = false; // show in GUI

        [Display(Name = "חיפוש לפי שם משתמש")]
        public string? UserNameSearch { get; set; }
        public bool DisplayUserNameSearch { get; set; } = false; // show in GUI
        //public DateTime? FilterFromDate { get; set; }
        private DateTime? _fromDate;
        public DateTime? FilterFromDate
        {
            get => _fromDate;
            set => _fromDate = ConvertToUtc(value);
        }
        public bool DisplayFilterDate { get; set; } = true; // show in GUI
        //public DateTime? FilterToDate { get; set; }
        private DateTime? _toDate;
        public DateTime? FilterToDate
        {
            get => _toDate;
            set => _toDate = ConvertToUtc(value);
        }

        [Display(Name ="הצג רק פעילים או לא סגורים")]
        public bool ShowActiveOrOpen { get; set; } = false;
        public bool DisplayShowActiveOrOpen { get; set; } = false; // show in GUI
        public string? SubjectSearch { get; set; }
        public bool DisplaySubjectSearch { get; set; } = false;  // show on GUI


        private DateTime? ConvertToUtc(DateTime? date)
        {
            if (!date.HasValue) return null;

            // Ensure the Kind is treated as Local (which is what the user picked)
            var localDate = date.Value.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(date.Value, DateTimeKind.Local)
                : date.Value.ToLocalTime();

            return localDate.ToUniversalTime();
        }
    }

    public enum SortDirection
    {
        Off,
        Ascending,
        Descending
    }
}