namespace Dashboard.Shared.Models;

public class SteamActivity
{
    public List<SteamGame> RecentlyPlayed { get; set; } = [];
}

public class SteamGame
{
    public string Name { get; set; } = "";
    public double HoursRecent { get; set; }
    public double HoursTotal { get; set; }
}
