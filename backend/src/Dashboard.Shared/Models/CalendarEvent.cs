namespace Dashboard.Shared.Models;

public class CalendarEvent
{
    public string Title    { get; set; } = "";
    public string Start    { get; set; } = "";
    public string End      { get; set; } = "";
    public bool   IsAllDay { get; set; }
    public string Location { get; set; } = "";
}
