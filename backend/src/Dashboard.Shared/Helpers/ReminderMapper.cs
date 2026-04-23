namespace Dashboard.Shared.Helpers;

public static class ReminderMapper
{
    public static Models.Reminder Map(
        string id, string title, string category,
        string dueDate, string recurring, DateTime today)
    {
        var d = DateTime.TryParse(dueDate, out var parsed) ? parsed : today;
        var daysUntil = (int)(d.Date - today).TotalDays;
        return new Models.Reminder
        {
            Id = id,
            Title = title,
            Category = category,
            DueDate = dueDate,
            Recurring = recurring,
            Status = daysUntil < 0 ? "overdue" : "upcoming",
            DaysUntilDue = daysUntil,
        };
    }
}
