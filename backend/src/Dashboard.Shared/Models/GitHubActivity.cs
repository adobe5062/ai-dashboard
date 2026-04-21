namespace Dashboard.Shared.Models;

public class GitHubActivity
{
    public int ReposActiveYesterday { get; set; }
    public List<GitHubCommit> RecentCommits { get; set; } = [];
}

public class GitHubCommit
{
    public string Repo { get; set; } = "";
    public string Message { get; set; } = "";
    public string Time { get; set; } = "";
}
