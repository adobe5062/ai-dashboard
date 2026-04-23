namespace Dashboard.Shared.Models;

public class TmdbHorrorCandidate
{
    public int TmdbId { get; set; }
    public string Title { get; set; } = "";
    public string Year { get; set; } = "";
    public string Overview { get; set; } = "";
    public double VoteAverage { get; set; }
    public string PosterPath { get; set; } = "";
}
