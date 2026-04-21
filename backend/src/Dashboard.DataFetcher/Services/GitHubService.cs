using Dashboard.Shared.Models;
using System.Text.Json.Nodes;

namespace Dashboard.DataFetcher.Services;

public class GitHubService
{
    private readonly HttpClient _http;
    private readonly string _username;

    public GitHubService(HttpClient http)
    {
        _http = http;
        _username = Environment.GetEnvironmentVariable("GITHUB_USERNAME") ?? "adobe5062";
    }

    public async Task<GitHubActivity> FetchAsync()
    {
        var url = $"https://api.github.com/users/{_username}/events/public?per_page=30";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "ai-dashboard/1.0");
        request.Headers.Add("Accept", "application/vnd.github+json");

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var events = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();

        var yesterday = DateTime.UtcNow.Date.AddDays(-1);

        var recentPushes = events
            .Where(e =>
                e!["type"]?.GetValue<string>() == "PushEvent" &&
                DateTime.TryParse(e["created_at"]?.GetValue<string>(), out var dt) &&
                dt.Date >= yesterday)
            .ToList();

        var commits = recentPushes
            .SelectMany(e =>
            {
                var repo = e!["repo"]!["name"]!.GetValue<string>().Split('/').Last();
                return e["payload"]!["commits"]!.AsArray()
                    .Take(2)
                    .Select(c => new GitHubCommit
                    {
                        Repo = repo,
                        Message = c!["message"]?.GetValue<string>()?.Split('\n')[0] ?? "",
                        Time = "yesterday",
                    });
            })
            .Take(5)
            .ToList();

        var activeRepos = recentPushes
            .Select(e => e!["repo"]!["name"]!.GetValue<string>())
            .Distinct()
            .Count();

        return new GitHubActivity
        {
            ReposActiveYesterday = activeRepos,
            RecentCommits = commits,
        };
    }
}
