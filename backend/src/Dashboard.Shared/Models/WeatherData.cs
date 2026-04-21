namespace Dashboard.Shared.Models;

public class WeatherData
{
    public int Temp { get; set; }
    public string Condition { get; set; } = "";
    public int Humidity { get; set; }
    public List<ForecastDay> Forecast { get; set; } = [];
}

public class ForecastDay
{
    public string Day { get; set; } = "";
    public int High { get; set; }
    public int Low { get; set; }
    public string Condition { get; set; } = "";
}
