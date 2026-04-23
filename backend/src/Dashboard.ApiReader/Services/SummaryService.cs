using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Dashboard.Shared.Models;
using System.Text.Json;

namespace Dashboard.ApiReader.Services;

public class SummaryService
{
    private readonly IAmazonDynamoDB _dynamo;
    private readonly string _table;

    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

    public SummaryService(IAmazonDynamoDB dynamo)
    {
        _dynamo = dynamo;
        _table  = Environment.GetEnvironmentVariable("SUMMARIES_TABLE")!;
    }

    public async Task<DashboardRecord?> GetLatestAsync()
    {
        var today     = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var yesterday = DateTime.UtcNow.AddDays(-1).ToString("yyyy-MM-dd");

        foreach (var date in new[] { today, yesterday })
        {
            var result = await _dynamo.GetItemAsync(new GetItemRequest
            {
                TableName = _table,
                Key       = new Dictionary<string, AttributeValue> { ["date"] = new() { S = date } },
            });

            if (result.Item.Count > 0 && result.Item.TryGetValue("payload", out var payload))
                return JsonSerializer.Deserialize<DashboardRecord>(payload.S, CaseInsensitive);
        }

        return null;
    }
}
