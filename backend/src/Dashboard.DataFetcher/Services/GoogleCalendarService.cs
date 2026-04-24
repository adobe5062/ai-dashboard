using Amazon.SimpleSystemsManagement;
using Dashboard.Shared.Models;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;

namespace Dashboard.DataFetcher.Services;

public class GoogleCalendarService
{
    private readonly HttpClient _http;
    private readonly IAmazonSimpleSystemsManagement _ssm;

    private static readonly TimeZoneInfo LocalTz = GetLocalTz();

    public GoogleCalendarService(HttpClient http, IAmazonSimpleSystemsManagement ssm)
    {
        _http = http;
        _ssm  = ssm;
    }

    public async Task<List<CalendarEvent>> FetchTodayAsync()
    {
        var token = await GetAccessTokenAsync();

        var today   = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, LocalTz).Date;
        var timeMin = TimeZoneInfo.ConvertTimeToUtc(today,            LocalTz).ToString("yyyy-MM-ddTHH:mm:ssZ");
        var timeMax = TimeZoneInfo.ConvertTimeToUtc(today.AddDays(1), LocalTz).ToString("yyyy-MM-ddTHH:mm:ssZ");

        var url = "https://www.googleapis.com/calendar/v3/calendars/primary/events" +
                  $"?timeMin={Uri.EscapeDataString(timeMin)}" +
                  $"&timeMax={Uri.EscapeDataString(timeMax)}" +
                  "&singleEvents=true&orderBy=startTime";

        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json  = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        var items = json["items"]?.AsArray() ?? [];

        return items
            .Where(i => i != null)
            .Select(i => new CalendarEvent
            {
                Title    = i!["summary"]?.GetValue<string>() ?? "(No title)",
                Start    = i["start"]?["dateTime"]?.GetValue<string>()
                           ?? i["start"]?["date"]?.GetValue<string>() ?? "",
                End      = i["end"]?["dateTime"]?.GetValue<string>()
                           ?? i["end"]?["date"]?.GetValue<string>() ?? "",
                IsAllDay = i["start"]?["dateTime"] == null,
                Location = i["location"]?.GetValue<string>() ?? "",
            })
            .ToList();
    }

    private async Task<string> GetAccessTokenAsync()
    {
        var clientId     = await _ssm.GetDecryptedAsync(Environment.GetEnvironmentVariable("SSM_GOOGLE_CLIENT_ID")!);
        var clientSecret = await _ssm.GetDecryptedAsync(Environment.GetEnvironmentVariable("SSM_GOOGLE_CLIENT_SECRET")!);
        var refreshToken = await _ssm.GetDecryptedAsync(Environment.GetEnvironmentVariable("SSM_GOOGLE_REFRESH_TOKEN")!);

        var response = await _http.PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"]     = clientId,
                ["client_secret"] = clientSecret,
                ["refresh_token"] = refreshToken,
                ["grant_type"]    = "refresh_token",
            }));

        response.EnsureSuccessStatusCode();
        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
        return json["access_token"]!.GetValue<string>();
    }

    private static TimeZoneInfo GetLocalTz()
    {
        try   { return TimeZoneInfo.FindSystemTimeZoneById("America/New_York"); }
        catch { return TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"); }
    }
}
