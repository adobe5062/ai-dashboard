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
            return (request.HttpMethod, request.Resource) switch
            {
                ("GET", "/dashboard")              => await GetDashboard(),
                ("POST", "/reminders")              => await CreateReminder(request),
                ("POST", "/reminders/{id}/complete") => await CompleteReminder(request),
                ("DELETE", "/reminders/{id}")        => await DeleteReminder(request),
                _ => NotFound("Route not found"),
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error handling {request.HttpMethod} {request.Resource}: {ex.Message}");
            return Error("Internal server error");
        }
    }

    private async Task<APIGatewayProxyResponse> GetDashboard()
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

    private async Task<APIGatewayProxyResponse> CreateReminder(APIGatewayProxyRequest request)
    {
        var req = JsonSerializer.Deserialize<ReminderCreateRequest>(
            request.Body ?? "{}", new JsonSerializerOptions(JsonSerializerDefaults.Web));

        if (req is null || string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.DueDate))
        {
            return BadRequest("title and dueDate are required");
        }

        var created = await _reminders.CreateAsync(
            req.Title.Trim(), req.Category?.Trim() ?? "", req.DueDate.Trim(), req.Recurring?.Trim() ?? "");

        return Ok(JsonSerializer.Serialize(created, JsonOptions));
    }

    private async Task<APIGatewayProxyResponse> CompleteReminder(APIGatewayProxyRequest request)
    {
        var id = request.PathParameters["id"];
        var updated = await _reminders.CompleteAsync(id);

        return Ok(JsonSerializer.Serialize(new { deleted = updated is null, reminder = updated }, JsonOptions));
    }

    private async Task<APIGatewayProxyResponse> DeleteReminder(APIGatewayProxyRequest request)
    {
        var id = request.PathParameters["id"];
        await _reminders.DeleteAsync(id);

        return Ok(JsonSerializer.Serialize(new { deleted = true }, JsonOptions));
    }

    private static APIGatewayProxyResponse Ok(string body) => new()
    {
        StatusCode = 200,
        Body = body,
        Headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Cache-Control"] = "no-store",
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

    private static APIGatewayProxyResponse BadRequest(string message) => new()
    {
        StatusCode = 400,
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
