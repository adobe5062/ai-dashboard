using Dashboard.Shared.Models;
using System.Text.Json.Nodes;

namespace Dashboard.DataFetcher.Services;

public class DevToService
{
    private readonly HttpClient _http;
    private const int ArticleCount = 5;

    public DevToService(HttpClient http) => _http = http;

    public async Task<List<DevToArticle>> FetchAsync()
    {
        var url = $"https://dev.to/api/articles?tag=webdev&top=1&per_page={ArticleCount}";
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("User-Agent", "ai-dashboard/1.0");

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;

        return json.AsArray()
            .Select(a => new DevToArticle
            {
                Title = a!["title"]?.GetValue<string>() ?? "",
                Url = a["url"]?.GetValue<string>() ?? "https://dev.to",
                Tags = a["tag_list"]?.AsArray()
                    .Select(t => t!.GetValue<string>())
                    .ToList() ?? [],
            })
            .ToList();
    }
}
