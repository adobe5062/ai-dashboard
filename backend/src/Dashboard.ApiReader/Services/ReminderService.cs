using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Dashboard.Shared.Helpers;
using Dashboard.Shared.Models;

namespace Dashboard.ApiReader.Services;

public class ReminderService
{
    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _table;

    public ReminderService(IAmazonDynamoDB dynamo)
    {
        _dynamo = dynamo;
        _table  = Environment.GetEnvironmentVariable("REMINDERS_TABLE")!;
    }

    public async Task<List<Reminder>> GetAllAsync()
    {
        var today  = DateTime.UtcNow.Date;
        var result = await _dynamo.ScanAsync(new ScanRequest { TableName = _table });

        return result.Items
            .Select(item => ReminderMapper.Map(
                id:        item["id"].S,
                title:     item["title"].S,
                category:  item.GetValueOrDefault("category")?.S ?? "",
                dueDate:   item["dueDate"].S,
                recurring: item.GetValueOrDefault("recurring")?.S ?? "",
                today:     today))
            .OrderBy(r => r.DaysUntilDue)
            .ToList();
    }
}
