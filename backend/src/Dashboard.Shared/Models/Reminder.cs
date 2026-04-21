namespace Dashboard.Shared.Models;

public class Reminder
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Category { get; set; } = "";
    public string DueDate { get; set; } = "";
    public string Recurring { get; set; } = "";
    public string Status { get; set; } = "";
    public int DaysUntilDue { get; set; }
}
