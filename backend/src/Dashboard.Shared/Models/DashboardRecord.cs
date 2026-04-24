namespace Dashboard.Shared.Models;

public class DashboardRecord
{
    public string Date { get; set; } = "";
    public string GeneratedAt { get; set; } = "";
    public string AiSummary { get; set; } = "";
    public string QuizQuestion { get; set; } = "";
    public string QuizAnswer { get; set; } = "";
    public string BHorrorTitle { get; set; } = "";
    public string BHorrorYear { get; set; } = "";
    public string BHorrorWhy { get; set; } = "";
    public string BHorrorPosterUrl { get; set; } = "";
    public List<TmdbHorrorCandidate> TmdbHorrorCandidates { get; set; } = [];
    public WeatherData Weather { get; set; } = new();
    public List<HackerNewsItem> HackerNews { get; set; } = [];
    public List<DevToArticle> DevTo { get; set; } = [];
    public GitHubActivity GitHub { get; set; } = new();
    public SteamActivity Steam { get; set; } = new();
    public List<Reminder> Reminders { get; set; } = [];
    public List<CalendarEvent> CalendarEvents { get; set; } = [];
}
