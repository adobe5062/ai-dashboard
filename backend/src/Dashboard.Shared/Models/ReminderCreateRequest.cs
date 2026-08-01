namespace Dashboard.Shared.Models;

public class ReminderCreateRequest
{
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string Recurring { get; set; } = "";
}
