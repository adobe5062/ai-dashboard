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
        var tags    = new[] { "dotnet", "aws", "csharp", "webdev", "blazor" };
        var fetched = new List<DevToArticle>();
        var seen    = new HashSet<string>();

        foreach (var tag in tags)
        {
            if (fetched.Count >= ArticleCount) break;

            var url     = $"https://dev.to/api/articles?tag={tag}&top=1&per_page=3";
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Add("User-Agent", "ai-dashboard/1.0");

            var response = await _http.SendAsync(request);
            if (!response.IsSuccessStatusCode) continue;

            var json = JsonNode.Parse(await response.Content.ReadAsStringAsync())!;
            foreach (var a in json.AsArray())
            {
                if (fetched.Count >= ArticleCount) break;
                var title = a!["title"]?.GetValue<string>() ?? "";
                if (!seen.Add(title)) continue;

                fetched.Add(new DevToArticle
                {
                    Title = title,
                    Url   = a["url"]?.GetValue<string>() ?? "https://dev.to",
                    Tags  = a["tag_list"]?.AsArray()
                               .Select(t => t!.GetValue<string>())
                               .ToList() ?? [],
                });
            }
        }

        return fetched;
    }
}
