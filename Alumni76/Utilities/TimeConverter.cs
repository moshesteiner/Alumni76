namespace Alumni76.Utilities
{
    public static class TimeConverter
    {
        /// <summary>
        /// Converts a UTC DateTime retrieved from the database to Israel Standard Time (IST) for display.
        /// </summary>
        public static DateTime ConvertUtcToIsraelTime(DateTime utcDateTime)
        {
            // 1. Force Kind to UTC (essential for correct conversion, as DB often returns 'Unspecified')
            DateTime utcTime = DateTime.SpecifyKind(utcDateTime, DateTimeKind.Utc);

            // 2. Define Time Zone IDs
            const string windowsTimeZoneId = "Israel Standard Time";
            const string linuxTimeZoneId = "Asia/Jerusalem";

            TimeZoneInfo israelTimeZone;

            // 3. Safely find the Israel time zone using cross-platform checks
            if (!TimeZoneInfo.TryFindSystemTimeZoneById(windowsTimeZoneId, out israelTimeZone!) &&
                !TimeZoneInfo.TryFindSystemTimeZoneById(linuxTimeZoneId, out israelTimeZone!))
            {
                // Fallback: This returns UTC+2 (doesn't account for Daylight Savings, but is a last resort)
                return utcTime.AddHours(2);
            }

            // 4. Perform the conversion
            return TimeZoneInfo.ConvertTimeFromUtc(utcTime, israelTimeZone);
        }
    }
}