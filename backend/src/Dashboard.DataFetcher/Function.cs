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
    private readonly IAmazonS3 _s3;

    public Function()
    {
        var http = new HttpClient();
        var ssm = new AmazonSimpleSystemsManagementClient();
        _weather = new WeatherService(http, ssm);
        _hackerNews = new HackerNewsService(http);
        _devTo = new DevToService(http);
        _gitHub = new GitHubService(http);
        _steam = new SteamService(http, ssm);
        _s3 = new AmazonS3Client();
    }

    public async Task<FetchResult> FunctionHandler(object input, ILambdaContext context)
    {
        context.Logger.LogInformation("Starting data fetch");

        var fetchTasks = new Task[]
        {
            _weather.FetchAsync(),
            _hackerNews.FetchAsync(),
            _devTo.FetchAsync(),
            _gitHub.FetchAsync(),
            _steam.FetchAsync(),
        };

        await Task.WhenAll(fetchTasks);

        var record = new DashboardRecord
        {
            Date = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            GeneratedAt = DateTime.UtcNow.ToString("O"),
            Weather = await (Task<WeatherData>)fetchTasks[0],
            HackerNews = await (Task<List<HackerNewsItem>>)fetchTasks[1],
            DevTo = await (Task<List<DevToArticle>>)fetchTasks[2],
            GitHub = await (Task<GitHubActivity>)fetchTasks[3],
            Steam = await (Task<SteamActivity>)fetchTasks[4],
        };

        var bucket = Environment.GetEnvironmentVariable("S3_BUCKET")!;
        var key = $"raw/{record.Date}.json";

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            ContentBody = JsonSerializer.Serialize(record),
            ContentType = "application/json",
        });

        context.Logger.LogInformation($"Raw data saved to s3://{bucket}/{key}");

        return new FetchResult { BucketName = bucket, ObjectKey = key, Date = record.Date };
    }
}

public class FetchResult
{
    public string BucketName { get; set; } = "";
    public string ObjectKey { get; set; } = "";
    public string Date { get; set; } = "";
}
