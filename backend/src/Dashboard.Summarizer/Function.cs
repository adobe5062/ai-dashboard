using Amazon.BedrockRuntime;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Dashboard.Shared.Helpers;
using Dashboard.Shared.Models;
using Dashboard.Summarizer.Services;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Dashboard.Summarizer;

public class Function
{
    private readonly IAmazonS3 _s3;
    private readonly BedrockService _bedrock;
    private readonly IAmazonDynamoDB _dynamo;

    private static readonly JsonSerializerOptions CaseInsensitive = new() { PropertyNameCaseInsensitive = true };

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
            Key        = input.ObjectKey,
        });

        using var reader = new StreamReader(s3Response.ResponseStream);
        var record = JsonSerializer.Deserialize<DashboardRecord>(await reader.ReadToEndAsync(), CaseInsensitive)!;

        record.Reminders = await FetchRemindersAsync();

        context.Logger.LogInformation("Calling Bedrock for daily briefing, quiz, and B-horror pick");
        var briefingTask = _bedrock.GenerateBriefingAsync(record);
        var quizTask     = _bedrock.GenerateQuizAsync();
        var horrorTask   = _bedrock.GenerateBHorrorPickAsync(record.TmdbHorrorCandidates);

        await Task.WhenAll(briefingTask, quizTask, horrorTask);

        record.AiSummary = await briefingTask;
        (record.QuizQuestion, record.QuizAnswer) = await quizTask;

        var (horrorTitle, horrorWhy, horrorIdx) = await horrorTask;
        record.BHorrorTitle = horrorTitle;
        record.BHorrorWhy   = horrorWhy;
        if (horrorIdx >= 0 && horrorIdx < record.TmdbHorrorCandidates.Count)
        {
            var picked = record.TmdbHorrorCandidates[horrorIdx];
            record.BHorrorYear      = picked.Year;
            record.BHorrorPosterUrl = string.IsNullOrEmpty(picked.PosterPath)
                ? ""
                : $"https://image.tmdb.org/t/p/w342{picked.PosterPath}";
        }

        record.TmdbHorrorCandidates = [];

        await SaveToDynamoAsync(record);
        context.Logger.LogInformation($"Summary saved to DynamoDB for {record.Date}");
        return record.Date;
    }

    private async Task<List<Reminder>> FetchRemindersAsync()
    {
        var table  = Environment.GetEnvironmentVariable("REMINDERS_TABLE")!;
        var today  = DateTime.UtcNow.Date;
        var result = await _dynamo.ScanAsync(new ScanRequest { TableName = table });

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

    private async Task SaveToDynamoAsync(DashboardRecord record)
    {
        var table = Environment.GetEnvironmentVariable("SUMMARIES_TABLE")!;
        var ttl   = DateTimeOffset.UtcNow.AddHours(48).ToUnixTimeSeconds();

        await _dynamo.PutItemAsync(new PutItemRequest
        {
            TableName = table,
            Item = new Dictionary<string, AttributeValue>
            {
                ["date"]        = new() { S = record.Date },
                ["generatedAt"] = new() { S = record.GeneratedAt },
                ["aiSummary"]   = new() { S = record.AiSummary },
                ["payload"]     = new() { S = JsonSerializer.Serialize(record) },
                ["ttl"]         = new() { N = ttl.ToString() },
            },
        });
    }
}
