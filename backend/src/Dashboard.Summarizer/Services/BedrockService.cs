using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;
using Dashboard.Shared.Models;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Dashboard.Summarizer.Services;

public class BedrockService
{
    private readonly IAmazonBedrockRuntime _bedrock;
    private readonly string _modelId;

    private static readonly Regex QuestionPattern = new(@"QUESTION:\s*(.*?)\s*ANSWER:", RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex AnswerPattern   = new(@"ANSWER:\s*(.*)",              RegexOptions.Singleline | RegexOptions.Compiled);
    private static readonly Regex PickPattern     = new(@"PICK:\s*(\d+)",               RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WhyPattern      = new(@"WHY:\s*(.+)",                 RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public BedrockService(IAmazonBedrockRuntime bedrock)
    {
        _bedrock  = bedrock;
        _modelId  = Environment.GetEnvironmentVariable("BEDROCK_MODEL_ID")
                    ?? "anthropic.claude-haiku-20240307-v1:0";
    }

    public async Task<string> GenerateBriefingAsync(DashboardRecord record)
    {
        return await InvokeAsync(BuildBriefingRequest(BuildPrompt(record)));
    }

    public async Task<(string Question, string Answer)> GenerateQuizAsync()
    {
        var categories = new[] { "C# language features", "Blazor", "AWS services", "SQL/query design", ".NET runtime internals", "web API design", "async/await patterns", "data modeling" };
        var types      = new[] { "explain a concept", "spot the bug in a code snippet", "compare two approaches", "design a solution" };

        var category = categories[Random.Shared.Next(categories.Length)];
        var type     = types[Random.Shared.Next(types.Length)];

        var prompt = $"""
            You are generating a daily technical challenge for Adam, a .NET/C# and Blazor developer
            who also works with AWS and SQL/Oracle.

            Generate one interview-style challenge in the category: {category}
            Challenge type: {type}

            Rules:
            - The question should be genuinely challenging but answerable in a few minutes
            - If it's a code snippet, keep it short (under 15 lines), realistic, and clearly formatted
            - The answer should be thorough but concise — explain the WHY, not just the WHAT
            - Vary difficulty: sometimes tricky edge cases, sometimes foundational but nuanced

            Respond in exactly this format with no extra text:
            QUESTION:
            <the question or code snippet>

            ANSWER:
            <the answer and explanation>
            """;

        var text = await InvokeAsync(new { anthropic_version = "bedrock-2023-05-31", max_tokens = 600, messages = new[] { new { role = "user", content = prompt } } });

        var question = QuestionPattern.Match(text);
        var answer   = AnswerPattern.Match(text);

        return (
            question.Success ? question.Groups[1].Value.Trim() : text,
            answer.Success   ? answer.Groups[1].Value.Trim()   : ""
        );
    }

    public async Task<(string Title, string Why, int TmdbIndex)> GenerateBHorrorPickAsync(List<TmdbHorrorCandidate> candidates)
    {
        if (candidates.Count == 0)
            return ("", "", -1);

        var list   = string.Join("\n", candidates.Select((c, i) => $"{i + 1}. \"{c.Title}\" ({c.Year}) — {c.Overview}"));
        var prompt = $"""
            You are a cult horror film expert specializing in obscure B-movies and midnight cinema.

            From this list of real horror films, pick the single most interesting cult or B-movie pick.
            Prefer: practical effects, bizarre premises, cult followings, giallo, folk horror, body horror.
            Avoid: anything too mainstream or well-known (e.g. skip generic slashers with no distinctive quality).

            FILMS:
            {list}

            Respond in exactly this format with no extra text:
            PICK: <number from the list>
            WHY: <one punchy sentence — what makes it genuinely worth watching>
            """;

        var text = await InvokeAsync(new { anthropic_version = "bedrock-2023-05-31", max_tokens = 120, messages = new[] { new { role = "user", content = prompt } } });

        var pickMatch = PickPattern.Match(text);
        var whyMatch  = WhyPattern.Match(text);

        var idx = pickMatch.Success && int.TryParse(pickMatch.Groups[1].Value, out var n) ? n - 1 : 0;
        if (idx < 0 || idx >= candidates.Count) idx = 0;

        return (candidates[idx].Title, whyMatch.Success ? whyMatch.Groups[1].Value.Trim() : "", idx);
    }

    private async Task<string> InvokeAsync(object requestBody)
    {
        var response = await _bedrock.InvokeModelAsync(new InvokeModelRequest
        {
            ModelId     = _modelId,
            ContentType = "application/json",
            Accept      = "application/json",
            Body        = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(requestBody))),
        });

        return JsonNode.Parse(response.Body)!["content"]![0]!["text"]!.GetValue<string>().Trim();
    }

    private static object BuildBriefingRequest(string prompt) => new
    {
        anthropic_version = "bedrock-2023-05-31",
        max_tokens        = 400,
        messages          = new[] { new { role = "user", content = prompt } },
    };

    private static string BuildPrompt(DashboardRecord record) => $"""
        You are an intelligent morning briefing assistant for Adam, a .NET/C# and Blazor web developer
        who also does data analysis work with SQL/Oracle and builds on AWS. He follows AI tooling closely.

        Your job is NOT to restate the data below — the dashboard already displays it.
        Your job is to REASON across all sources and produce three things:

        1. RELEVANT ARTICLES (2-3 max): From the Hacker News and Dev.to lists, flag only the articles
           genuinely relevant to Adam's stack (.NET, C#, Blazor, AWS, SQL, web dev, data analysis, AI tools).
           Explain in one sentence WHY each one matters to him specifically. Skip anything irrelevant — fewer is better.

        2. CROSS-SOURCE INSIGHT (1-2 sentences): Connect dots the widgets can't. Examples:
           - A HN story that relates to his recent GitHub commits
           - A weather + reminder combination that changes urgency
           - A gaming pattern that contrasts with code output
           Be specific. If there's nothing interesting to connect, skip this section entirely.

        3. PRIORITY ACTION ITEMS (2-3 max): Concrete things to do today, ranked by urgency.
           Derive these by reasoning across reminders, GitHub inactivity, weather, and article relevance.
           Format as a tight numbered list.

        Be direct and dry. No fluff, no filler, no "Great news!". Under 180 words total.
        Use markdown bold for section headers (**ARTICLES**, **INSIGHT**, **TODAY**).

        DATA:
        WEATHER: {FormatWeather(record.Weather)}
        CALENDAR: {FormatCalendar(record.CalendarEvents)}
        HACKER NEWS: {FormatHackerNews(record.HackerNews)}
        DEV.TO: {FormatDevTo(record.DevTo)}
        GITHUB: {FormatGitHub(record.GitHub)}
        STEAM: {FormatSteam(record.Steam)}
        REMINDERS: {FormatReminders(record.Reminders)}
        """;

    private static string FormatCalendar(List<CalendarEvent>? events) =>
        events is null or { Count: 0 } ? "none"
            : string.Join("\n", events.Select(e =>
                e.IsAllDay
                    ? $"All day: {e.Title}"
                    : $"{ParseIsoLocal(e.Start)}–{ParseIsoLocal(e.End)}: {e.Title}{(string.IsNullOrEmpty(e.Location) ? "" : $" @ {e.Location}")}"));

    private static string ParseIsoLocal(string iso) =>
        DateTime.TryParse(iso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt)
            ? dt.ToLocalTime().ToString("h:mm tt")
            : iso;

    private static string FormatWeather(WeatherData? w) =>
        w is null ? "unavailable" : $"{w.Temp}°F, {w.Condition}, {w.Humidity}% humidity";

    private static string FormatHackerNews(List<HackerNewsItem>? items) =>
        items is null or { Count: 0 } ? "none"
            : string.Join("\n", items.Select((s, i) => $"{i + 1}. {s.Title} ({s.Points}pts) — {s.Url}"));

    private static string FormatDevTo(List<DevToArticle>? articles) =>
        articles is null or { Count: 0 } ? "none"
            : string.Join("\n", articles.Select((a, i) => $"{i + 1}. {a.Title} [tags: {string.Join(", ", a.Tags)}] — {a.Url}"));

    private static string FormatGitHub(GitHubActivity? gh) =>
        gh is null ? "unavailable"
            : gh.ReposActiveYesterday > 0
                ? $"{gh.ReposActiveYesterday} repos active. Commits: {string.Join(", ", gh.RecentCommits.Select(c => $"{c.Repo}: {c.Message}"))}"
                : "No commits yesterday.";

    private static string FormatSteam(SteamActivity? steam) =>
        steam is null or { RecentlyPlayed.Count: 0 } ? "No recent games."
            : string.Join(", ", steam.RecentlyPlayed.Select(g => $"{g.Name} ({g.HoursRecent}h recently)"));

    private static string FormatReminders(List<Reminder>? reminders) =>
        reminders is null or { Count: 0 } ? "none"
            : string.Join("\n", reminders.Select(r =>
                r.Status == "overdue"
                    ? $"OVERDUE: {r.Title} ({Math.Abs(r.DaysUntilDue)}d ago)"
                    : $"In {r.DaysUntilDue}d: {r.Title}"));
}
