using Amazon.Lambda.Core;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SimpleSystemsManagement;
using Dashboard.DataFetcher.Services;
using Dashboard.Shared.Models;
using System.Text.Json;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Dashboard.DataFetcher;

public class Function
{
    private readonly WeatherService _weather;
    private readonly HackerNewsService _hackerNews;
    private readonly DevToService _devTo;
    private readonly GitHubService _gitHub;
    private readonly SteamService _steam;
    private readonly TmdbService _tmdb;
    private readonly GoogleCalendarService _calendar;
    private readonly IAmazonS3 _s3;

    public Function()
    {
        var http = new HttpClient();
        var ssm  = new AmazonSimpleSystemsManagementClient();
        _weather    = new WeatherService(http, ssm);
        _hackerNews = new HackerNewsService(http);
        _devTo      = new DevToService(http);
        _gitHub     = new GitHubService(http);
        _steam      = new SteamService(http, ssm);
        _tmdb       = new TmdbService(http, ssm);
        _calendar   = new GoogleCalendarService(http, ssm);
        _s3         = new AmazonS3Client();
    }

    public async Task<FetchResult> FunctionHandler(object input, ILambdaContext context)
    {
        context.Logger.LogInformation("Starting data fetch");

        var weatherTask    = SafeFetch(_weather.FetchAsync(),           context, "Weather");
        var hackerNewsTask = SafeFetch(_hackerNews.FetchAsync(),        context, "HackerNews");
        var devToTask      = SafeFetch(_devTo.FetchAsync(),             context, "DevTo");
        var gitHubTask     = SafeFetch(_gitHub.FetchAsync(),            context, "GitHub");
        var steamTask      = SafeFetch(_steam.FetchAsync(),             context, "Steam");
        var tmdbTask       = SafeFetch(_tmdb.FetchCandidatesAsync(),    context, "TMDB");
        var calendarTask   = SafeFetch(_calendar.FetchTodayAsync(),     context, "GoogleCalendar");

        await Task.WhenAll(weatherTask, hackerNewsTask, devToTask, gitHubTask, steamTask, tmdbTask, calendarTask);

        var record = new DashboardRecord
        {
            Date                  = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            GeneratedAt           = DateTime.UtcNow.ToString("O"),
            Weather               = await weatherTask,
            HackerNews            = await hackerNewsTask,
            DevTo                 = await devToTask,
            GitHub                = await gitHubTask,
            Steam                 = await steamTask,
            TmdbHorrorCandidates  = await tmdbTask    ?? [],
            CalendarEvents        = await calendarTask ?? [],
        };

        var bucket = Environment.GetEnvironmentVariable("S3_BUCKET")!;
        var key    = $"raw/{record.Date}.json";

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName  = bucket,
            Key         = key,
            ContentBody = JsonSerializer.Serialize(record),
            ContentType = "application/json",
        });

        context.Logger.LogInformation($"Raw data saved to s3://{bucket}/{key}");
        return new FetchResult { BucketName = bucket, ObjectKey = key, Date = record.Date };
    }

    private static async Task<T?> SafeFetch<T>(Task<T> task, ILambdaContext context, string name) where T : class
    {
        try { return await task; }
        catch (Exception ex)
        {
            context.Logger.LogWarning($"{name} fetch failed: {ex.Message}");
            return null;
        }
    }
}
