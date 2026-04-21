using Amazon.BedrockRuntime;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Dashboard.Summarizer.Services;
using Dashboard.Shared.Models;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Dashboard.Summarizer;

public class Function
{
    private readonly IAmazonS3 _s3;
    private readonly BedrockService _bedrock;
    private readonly IAmazonDynamoDB _dynamo;

    public Function()
    {
        _s3 = new AmazonS3Client();
        _bedrock = new BedrockService(new AmazonBedrockRuntimeClient(
            Amazon.RegionEndpoint.GetBySystemName(
                Environment.GetEnvironmentVariable("BEDROCK_REGION") ?? "us-east-1")));
        _dynamo = new AmazonDynamoDBClient();
    }

    public async Task<string> FunctionHandler(FetchResult input, ILambdaContext context)
    {
        context.Logger.LogInformation($"Reading raw data from s3://{input.BucketName}/{input.ObjectKey}");

        var s3Response = await _s3.GetObjectAsync(new GetObjectRequest
        {
            BucketName = input.BucketName,
            Key = input.ObjectKey,
        });

        using var reader = new StreamReader(s3Response.ResponseStream);
        var json = await reader.ReadToEndAsync();
        var record = JsonSerializer.Deserialize<DashboardRecord>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
        })!;

        // Reminders are stored in DynamoDB — fetch before generating the AI summary
        record.Reminders = await FetchRemindersAsync();

        context.Logger.LogInformation("Calling Bedrock for daily briefing");
        record.AiSummary = await _bedrock.GenerateBriefingAsync(record);

        await SaveToDynamoAsync(record);

        context.Logger.LogInformation($"Summary saved to DynamoDB for {record.Date}");
        return record.Date;
    }

    private async Task<List<Reminder>> FetchRemindersAsync()
    {
        var table = Environment.GetEnvironmentVariable("REMINDERS_TABLE")!;
        var today = DateTime.UtcNow.Date;

        var result = await _dynamo.ScanAsync(new ScanRequest { TableName = table });

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

    private async Task SaveToDynamoAsync(DashboardRecord record)
    {
        var table = Environment.GetEnvironmentVariable("SUMMARIES_TABLE")!;
        var ttl = DateTimeOffset.UtcNow.AddHours(48).ToUnixTimeSeconds();

        var item = new Dictionary<string, AttributeValue>
        {
            ["date"] = new() { S = record.Date },
            ["generatedAt"] = new() { S = record.GeneratedAt },
            ["aiSummary"] = new() { S = record.AiSummary },
            ["payload"] = new() { S = JsonSerializer.Serialize(record) },
            ["ttl"] = new() { N = ttl.ToString() },
        };

        await _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = table,
            Item = item,
        });
    }
}

public class FetchResult
{
    public string BucketName { get; set; } = "";
    public string ObjectKey { get; set; } = "";
    public string Date { get; set; } = "";
}
