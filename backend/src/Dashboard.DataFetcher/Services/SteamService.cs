using Amazon.SimpleSystemsManagement;
using Dashboard.Shared.Models;
using System.Text.Json.Nodes;

namespace Dashboard.DataFetcher.Services;

public class SteamService
{
    private readonly HttpClient _http;
    private readonly IAmazonSimpleSystemsManagement _ssm;

    public SteamService(HttpClient http, IAmazonSimpleSystemsManagement ssm)
    {
        _http = http;
        _ssm = ssm;
    }

    public async Task<SteamActivity> FetchAsync()
    {
        var apiKey = await _ssm.GetDecryptedAsync(Environment.GetEnvironmentVariable("SSM_STEAM_KEY")!);
        var userId = await _ssm.GetDecryptedAsync(Environment.GetEnvironmentVariable("SSM_STEAM_USER_ID")!);

        var url  = $"https://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v1/?key={apiKey}&steamid={userId}&count=5";
        var json = JsonNode.Parse(await _http.GetStringAsync(url))!;
        var games = json["response"]?["games"]?.AsArray() ?? [];

        return new SteamActivity
        {
            RecentlyPlayed = games
                .Select(g => new SteamGame
                {
                    AppId       = int.TryParse(g!["appid"]?.ToString(), out var aid) ? aid : 0,
                    ImgIconUrl  = g["img_icon_url"]?.GetValue<string>() ?? "",
                    Name        = g["name"]?.GetValue<string>() ?? "Unknown",
                    HoursRecent = Math.Round(g["playtime_2weeks"]?.GetValue<int>() / 60.0 ?? 0, 1),
                    HoursTotal  = Math.Round(g["playtime_forever"]?.GetValue<int>() / 60.0 ?? 0, 1),
                })
                .ToList(),
        };
    }
}
