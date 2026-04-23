using Amazon.SimpleSystemsManagement;
using Dashboard.Shared.Models;
using System.Text.Json.Nodes;

namespace Dashboard.DataFetcher.Services;

public class TmdbService
{
    private readonly HttpClient _http;
    private readonly IAmazonSimpleSystemsManagement _ssm;

    private static readonly (int Start, int End)[] Eras =
    [
        (1965, 1969), (1970, 1979), (1980, 1989), (1990, 1999), (2000, 2009),
    ];

    public TmdbService(HttpClient http, IAmazonSimpleSystemsManagement ssm)
    {
        _http = http;
        _ssm = ssm;
    }

    public async Task<List<TmdbHorrorCandidate>> FetchCandidatesAsync()
    {
        var apiKey = await _ssm.GetDecryptedAsync(Environment.GetEnvironmentVariable("SSM_TMDB_KEY")!);

        var era  = Eras[Random.Shared.Next(Eras.Length)];
        var page = Random.Shared.Next(1, 6);

        var queryParams = new Dictionary<string, string>
        {
            ["api_key"]                       = apiKey,
            ["with_genres"]                   = "27",
            ["sort_by"]                       = "popularity.asc",
            ["vote_count.gte"]                = "50",
            ["vote_average.gte"]              = "5.0",
            ["primary_release_date.gte"]      = $"{era.Start}-01-01",
            ["primary_release_date.lte"]      = $"{era.End}-12-31",
            ["page"]                          = page.ToString(),
        };
        var url = "https://api.themoviedb.org/3/discover/movie?" +
            string.Join("&", queryParams.Select(kv => $"{kv.Key}={kv.Value}"));

        var json    = JsonNode.Parse(await _http.GetStringAsync(url))!;
        var results = json["results"]!.AsArray();

        return results
            .Take(12)
            .Where(r => r != null && r["title"] != null)
            .Select(r => new TmdbHorrorCandidate
            {
                TmdbId      = r!["id"]!.GetValue<int>(),
                Title       = r["title"]!.GetValue<string>(),
                Year        = r["release_date"]?.GetValue<string>().Split('-')[0] ?? "",
                Overview    = Truncate(r["overview"]?.GetValue<string>() ?? "", 120),
                VoteAverage = r["vote_average"]?.GetValue<double>() ?? 0,
                PosterPath  = r["poster_path"]?.GetValue<string>() ?? "",
            })
            .ToList();
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max].TrimEnd() + "…";
}
