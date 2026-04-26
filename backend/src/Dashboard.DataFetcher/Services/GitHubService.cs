using Amazon.SimpleSystemsManagement;
using Dashboard.Shared.Models;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace Dashboard.DataFetcher.Services;

public class GitHubService
{
    private readonly HttpClient _http;
    private readonly IAmazonSimpleSystemsManagement _ssm;
    private readonly string _username;

    public GitHubService(HttpClient http, IAmazonSimpleSystemsManagement ssm)
    {
        _http     = http;
        _ssm      = ssm;
        _username = Environment.GetEnvironmentVariable("GITHUB_USERNAME") ?? "adobe5062";
    }

    public async Task<GitHubActivity> FetchAsync()
    {
        var tokenParam = Environment.GetEnvironmentVariable("SSM_GITHUB_TOKEN");
        var token      = string.IsNullOrEmpty(tokenParam) ? null : await _ssm.GetDecryptedAsync(tokenParam);

        var eventsResp = await GetAsync($"https://api.github.com/users/{_username}/events?per_page=100", token);
        eventsResp.EnsureSuccessStatusCode();

        var events = JsonNode.Parse(await eventsResp.Content.ReadAsStringAsync())!.AsArray();
        Console.WriteLine($"[GitHub] total events: {events.Count}");

        var twoDaysAgo = DateTime.UtcNow.Date.AddDays(-2);

        var recentPushes = events
            .Where(e =>
                e!["type"]?.GetValue<string>() == "PushEvent" &&
                DateTime.TryParse(e["created_at"]?.GetValue<string>(), out var dt) &&
                dt.Date >= twoDaysAgo)
            .ToList();

        foreach (var p in recentPushes)
        {
            var repo        = p!["repo"]?["name"]?.GetValue<string>() ?? "?";
            var commitsNode = p["payload"]?["commits"]?.AsArray();
            Console.WriteLine($"[GitHub] push to {repo} — commits in payload: {commitsNode?.Count ?? -1}");
        }

        var commits = new List<GitHubCommit>();

        foreach (var e in recentPushes)
        {
            var fullRepo  = e!["repo"]?["name"]?.GetValue<string>() ?? "unknown";
            var repoShort = fullRepo.Split('/').Last();
            var payload   = e["payload"];

            var inlineCommits = payload?["commits"]?.AsArray();
            if (inlineCommits is { Count: > 0 })
            {
                commits.AddRange(inlineCommits.Take(2).Select(c => new GitHubCommit
                {
                    Repo    = repoShort,
                    Message = c!["message"]?.GetValue<string>()?.Split('\n')[0] ?? "",
                    Time    = "yesterday",
                }));
            }
            else
            {
                // payload.commits is empty — fall back to the Compare API
                var head   = payload?["head"]?.GetValue<string>();
                var before = payload?["before"]?.GetValue<string>();

                if (!string.IsNullOrEmpty(head))
                {
                    var fetched = await FetchCommitsViaCompareAsync(fullRepo, repoShort, head, before, token);
                    commits.AddRange(fetched);
                }
            }

            if (commits.Count >= 5) break;
        }

        var activeRepos = recentPushes
            .Select(e => e!["repo"]!["name"]!.GetValue<string>())
            .Distinct()
            .Count();

        return new GitHubActivity
        {
            ReposActiveYesterday = activeRepos,
            RecentCommits        = commits.Take(5).ToList(),
        };
    }

    private async Task<List<GitHubCommit>> FetchCommitsViaCompareAsync(
        string fullRepo, string repoShort, string head, string? before, string? token)
    {
        const string zeros = "0000000000000000000000000000000000000000";

        if (!string.IsNullOrEmpty(before) && before != zeros)
        {
            var resp = await GetAsync(
                $"https://api.github.com/repos/{fullRepo}/compare/{before}...{head}", token);

            if (resp.IsSuccessStatusCode)
            {
                var data    = JsonNode.Parse(await resp.Content.ReadAsStringAsync());
                var commits = data?["commits"]?.AsArray();
                if (commits is { Count: > 0 })
                {
                    return commits.Take(2).Select(c => new GitHubCommit
                    {
                        Repo    = repoShort,
                        Message = c!["commit"]?["message"]?.GetValue<string>()?.Split('\n')[0] ?? "",
                        Time    = "yesterday",
                    }).ToList();
                }
            }
        }

        // Last resort: single commit at head
        var singleResp = await GetAsync(
            $"https://api.github.com/repos/{fullRepo}/commits/{head}", token);

        if (singleResp.IsSuccessStatusCode)
        {
            var data = JsonNode.Parse(await singleResp.Content.ReadAsStringAsync());
            var msg  = data?["commit"]?["message"]?.GetValue<string>()?.Split('\n')[0];
            if (!string.IsNullOrEmpty(msg))
                return [new GitHubCommit { Repo = repoShort, Message = msg, Time = "yesterday" }];
        }

        return [];
    }

    private async Task<HttpResponseMessage> GetAsync(string url, string? token)
    {
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("User-Agent", "ai-dashboard/1.0");
        req.Headers.Add("Accept", "application/vnd.github+json");
        if (!string.IsNullOrEmpty(token))
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _http.SendAsync(req);
    }
}
