using System.Text.RegularExpressions;

namespace Dashboard.Shared.Helpers;

public static class RecurrenceHelper
{
    /// <summary>
    /// Returns the next due date (yyyy-MM-dd) for a recurring reminder, or null if it
    /// doesn't recur (caller should delete the reminder instead of rescheduling it).
    /// </summary>
    public static string? NextDueDate(string currentDueDate, string recurring)
    {
        if (string.IsNullOrWhiteSpace(recurring))
            return null;

        var baseDate = DateTime.TryParse(currentDueDate, out var parsed) ? parsed : DateTime.UtcNow.Date;
        var text = recurring.Trim().ToLowerInvariant();

        var match = Regex.Match(text, @"every\s+(\d+)\s+(day|week|month|year)s?");
        if (match.Success)
        {
            var n = int.Parse(match.Groups[1].Value);
            DateTime? next = match.Groups[2].Value switch
            {
                "day"   => baseDate.AddDays(n),
                "week"  => baseDate.AddDays(n * 7),
                "month" => baseDate.AddMonths(n),
                "year"  => baseDate.AddYears(n),
                _       => null,
            };
            return next?.ToString("yyyy-MM-dd");
        }

        return text switch
        {
            "daily"                => baseDate.AddDays(1).ToString("yyyy-MM-dd"),
            "weekly"               => baseDate.AddDays(7).ToString("yyyy-MM-dd"),
            "monthly"              => baseDate.AddMonths(1).ToString("yyyy-MM-dd"),
            "annually" or "yearly" => baseDate.AddYears(1).ToString("yyyy-MM-dd"),
            _                      => null,
        };
    }
}
