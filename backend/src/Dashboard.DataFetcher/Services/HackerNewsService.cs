using Dashboard.Shared.Models;
using System.Text.Json.Nodes;

namespace Dashboard.DataFetcher.Services;

public class HackerNewsService
{
    private readonly HttpClient _http;
    private const int TopStoriesCount = 5;

    public HackerNewsService(HttpClient http) => _http = http;

    public async Task<List<HackerNewsItem>> FetchAsync()
    {
        var idsJson = await _http.GetStringAsync("https://hacker-news.firebaseio.com/v0/topstories.json");
        var ids = JsonNode.Parse(idsJson)!.AsArray()
            .Take(20)
            .Select(n => n!.GetValue<int>())
            .ToList();

        var tasks = ids.Select(FetchItem);
        var items = await Task.WhenAll(tasks);

        return items
            .Where(i => i is not null)
            .Take(TopStoriesCount)
            .ToList()!;
    }

    private async Task<HackerNewsItem?> FetchItem(int id)
    {
        try
        {
            var json = JsonNode.Parse(await _http.GetStringAsync(
                $"https://hacker-news.firebaseio.com/v0/item/{id}.json"))!;

            var type = json["type"]?.GetValue<string>();
            if (type != "story") return null;

            return new HackerNewsItem
            {
                Title = json["title"]?.GetValue<string>() ?? "",
                Url = json["url"]?.GetValue<string>() ?? $"https://news.ycombinator.com/item?id={id}",
                Points = json["score"]?.GetValue<int>() ?? 0,
            };
        }
        catch
        {
            return null;
        }
    }
}
