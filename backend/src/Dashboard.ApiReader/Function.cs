using Amazon.DynamoDBv2;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Dashboard.ApiReader.Services;
using Dashboard.Shared.Models;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Dashboard.ApiReader;

public class Function
{
    private readonly SummaryService _summaries;
    private readonly ReminderService _reminders;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public Function()
    {
        var dynamo = new AmazonDynamoDBClient();
        _summaries = new SummaryService(dynamo);
        _reminders = new ReminderService(dynamo);
    }

    public async Task<APIGatewayProxyResponse> FunctionHandler(
        APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            var summaryTask = _summaries.GetLatestAsync();
            var remindersTask = _reminders.GetAllAsync();

            await Task.WhenAll(summaryTask, remindersTask);

            var record = await summaryTask;
            var reminders = await remindersTask;

            if (record is null)
            {
                return NotFound("No dashboard data available yet. The pipeline runs daily at 7am ET.");
            }

            // Reminders from the live table override whatever was stored in the summary
            record.Reminders = reminders;

            return Ok(JsonSerializer.Serialize(record, JsonOptions));
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error reading dashboard data: {ex.Message}");
            return Error("Internal server error");
        }
    }

    private static APIGatewayProxyResponse Ok(string body) => new()
    {
        StatusCode = 200,
        Body = body,
        Headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Cache-Control"] = "max-age=3600",
            ["Access-Control-Allow-Origin"] = "*",
        },
    };

    private static APIGatewayProxyResponse NotFound(string message) => new()
    {
        StatusCode = 404,
        Body = JsonSerializer.Serialize(new { message }),
        Headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Access-Control-Allow-Origin"] = "*",
        },
    };

    private static APIGatewayProxyResponse Error(string message) => new()
    {
        StatusCode = 500,
        Body = JsonSerializer.Serialize(new { message }),
        Headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Access-Control-Allow-Origin"] = "*",
        },
    };
}
