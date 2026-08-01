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

    public async Task<Reminder> CreateAsync(string title, string category, string dueDate, string recurring)
    {
        var id = $"rem_{Guid.NewGuid():N}"[..12];

        await _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = _table,
            Item = new Dictionary<string, AttributeValue>
            {
                ["id"]        = new AttributeValue { S = id },
                ["title"]     = new AttributeValue { S = title },
                ["category"]  = new AttributeValue { S = category },
                ["dueDate"]   = new AttributeValue { S = dueDate },
                ["recurring"] = new AttributeValue { S = recurring },
            },
        });

        return ReminderMapper.Map(id, title, category, dueDate, recurring, DateTime.UtcNow.Date);
    }

    public async Task DeleteAsync(string id)
    {
        await _dynamo.DeleteItemAsync(new DeleteItemRequest
        {
            TableName = _table,
            Key = new Dictionary<string, AttributeValue> { ["id"] = new AttributeValue { S = id } },
        });
    }

    /// <summary>
    /// Marks a reminder done. Recurring reminders are rescheduled to their next
    /// due date and returned; non-recurring reminders are deleted (returns null).
    /// </summary>
    public async Task<Reminder?> CompleteAsync(string id)
    {
        var getResponse = await _dynamo.GetItemAsync(new GetItemRequest
        {
            TableName = _table,
            Key = new Dictionary<string, AttributeValue> { ["id"] = new AttributeValue { S = id } },
        });

        if (getResponse.Item is null || getResponse.Item.Count == 0)
            return null;

        var title     = getResponse.Item["title"].S;
        var category  = getResponse.Item.GetValueOrDefault("category")?.S ?? "";
        var recurring = getResponse.Item.GetValueOrDefault("recurring")?.S ?? "";
        var dueDate   = getResponse.Item["dueDate"].S;

        var nextDueDate = RecurrenceHelper.NextDueDate(dueDate, recurring);
        if (nextDueDate is null)
        {
            await DeleteAsync(id);
            return null;
        }

        await _dynamo.UpdateItemAsync(new UpdateItemRequest
        {
            TableName = _table,
            Key = new Dictionary<string, AttributeValue> { ["id"] = new AttributeValue { S = id } },
            UpdateExpression = "SET dueDate = :d",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":d"] = new AttributeValue { S = nextDueDate },
            },
        });

        return ReminderMapper.Map(id, title, category, nextDueDate, recurring, DateTime.UtcNow.Date);
    }
}
