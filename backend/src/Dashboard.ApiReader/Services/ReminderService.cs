using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Dashboard.Shared.Models;

namespace Dashboard.ApiReader.Services;

public class ReminderService
{
    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _table;

    public ReminderService(IAmazonDynamoDB dynamo)
    {
        _dynamo = dynamo;
        _table = Environment.GetEnvironmentVariable("REMINDERS_TABLE")!;
    }

    public async Task<List<Reminder>> GetAllAsync()
    {
        var today = DateTime.UtcNow.Date;
        var result = await _dynamo.ScanAsync(new ScanRequest { TableName = _table });

        return result.Items
            .Select(item =>
            {
                var dueDate = DateTime.TryParse(item["dueDate"].S, out var d) ? d : today;
                var daysUntil = (int)(dueDate.Date - today).TotalDays;
                return new Reminder
                {
                    Id = item["id"].S,
                    Title = item["title"].S,
                    Category = item.GetValueOrDefault("category")?.S ?? "",
                    DueDate = item["dueDate"].S,
                    Recurring = item.GetValueOrDefault("recurring")?.S ?? "",
                    Status = daysUntil < 0 ? "overdue" : "upcoming",
                    DaysUntilDue = daysUntil,
                };
            })
            .OrderBy(r => r.DaysUntilDue)
            .ToList();
    }
}
