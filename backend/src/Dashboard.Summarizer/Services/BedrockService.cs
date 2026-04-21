using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Dashboard.Shared.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Dashboard.Summarizer.Services;

public class BedrockService
{
    private readonly IAmazonBedrockRuntime _bedrock;
    private readonly string _modelId;

    public BedrockService(IAmazonBedrockRuntime bedrock)
    {
        _bedrock = bedrock;
        _modelId = Environment.GetEnvironmentVariable("BEDROCK_MODEL_ID")
            ?? "anthropic.claude-haiku-20240307-v1:0";
    }

    public async Task<string> GenerateBriefingAsync(DashboardRecord record)
    {
        var prompt = BuildPrompt(record);

        var body = JsonSerializer.Serialize(new
        {
            anthropic_version = "bedrock-2023-05-31",
            max_tokens = 200,
            messages = new[]
            {
                new { role = "user", content = prompt }
            }
        });

        var response = await _bedrock.InvokeModelAsync(new InvokeModelRequest
        {
            ModelId = _modelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(body)),
        });

        var responseJson = JsonNode.Parse(response.Body)!;
        return responseJson["content"]![0]!["text"]!.GetValue<string>().Trim();
    }

    private static string BuildPrompt(DashboardRecord record)
    {
        var weather = $"{record.Weather.Temp}°F, {record.Weather.Condition}, {record.Weather.Humidity}% humidity";

        var hnStories = string.Join("\n", record.HackerNews.Select((s, i) => $"{i + 1}. {s.Title} ({s.Points}pts)"));

        var devToArticles = string.Join("\n", record.DevTo.Select((a, i) => $"{i + 1}. {a.Title}"));

        var github = record.GitHub.ReposActiveYesterday > 0
            ? $"{record.GitHub.ReposActiveYesterday} repos active. Commits: " +
              string.Join(", ", record.GitHub.RecentCommits.Select(c => $"{c.Repo}: {c.Message}"))
            : "No commits yesterday.";

        var steam = record.Steam.RecentlyPlayed.Count > 0
            ? string.Join(", ", record.Steam.RecentlyPlayed.Select(g => $"{g.Name} ({g.HoursRecent}h recently)"))
            : "No recent games.";

        var reminders = string.Join("\n", record.Reminders.Select(r =>
            r.Status == "overdue"
                ? $"OVERDUE: {r.Title} ({Math.Abs(r.DaysUntilDue)}d ago)"
                : $"In {r.DaysUntilDue}d: {r.Title}"));

        return $"""
            You are a personal morning briefing assistant for a software developer named Adam.
            Be direct, dry, and slightly sardonic in tone. No fluff. Keep it under 120 words.

            Summarize the following into a morning briefing:

            WEATHER (Maryland): {weather}
            TOP HACKER NEWS:
            {hnStories}
            TOP DEV.TO ARTICLES:
            {devToArticles}
            GITHUB ACTIVITY: {github}
            RECENTLY PLAYED (Steam): {steam}
            UPCOMING REMINDERS:
            {reminders}

            Write the briefing now. Start directly — no greeting, no "Here is your briefing".
            """;
    }
}
