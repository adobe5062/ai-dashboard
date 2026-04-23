using Amazon.SimpleSystemsManagement;
using Dashboard.Shared.Models;
using System.Text.Json.Nodes;

namespace Dashboard.DataFetcher.Services;

public class WeatherService
{
    private readonly HttpClient _http;
    private readonly IAmazonSimpleSystemsManagement _ssm;

    public WeatherService(HttpClient http, IAmazonSimpleSystemsManagement ssm)
    {
        _http = http;
        _ssm = ssm;
    }

    public async Task<WeatherData> FetchAsync()
    {
        var apiKey = await _ssm.GetDecryptedAsync(Environment.GetEnvironmentVariable("SSM_WEATHER_KEY")!);
        var lat    = await _ssm.GetDecryptedAsync(Environment.GetEnvironmentVariable("SSM_WEATHER_LAT")!);
        var lon    = await _ssm.GetDecryptedAsync(Environment.GetEnvironmentVariable("SSM_WEATHER_LON")!);

        var currentUrl  = $"https://api.openweathermap.org/data/2.5/weather?lat={lat}&lon={lon}&appid={apiKey}&units=imperial";
        var forecastUrl = $"https://api.openweathermap.org/data/2.5/forecast?lat={lat}&lon={lon}&appid={apiKey}&units=imperial&cnt=24";

        var currentJson  = JsonNode.Parse(await _http.GetStringAsync(currentUrl))!;
        var forecastJson = JsonNode.Parse(await _http.GetStringAsync(forecastUrl))!;

        var weather = new WeatherData
        {
            Temp      = (int)Math.Round(currentJson["main"]!["temp"]!.GetValue<double>()),
            Condition = currentJson["weather"]![0]!["main"]!.GetValue<string>(),
            Humidity  = currentJson["main"]!["humidity"]!.GetValue<int>(),
            Forecast  = forecastJson["list"]!.AsArray()
                .Where(e => e!["dt_txt"]!.GetValue<string>().EndsWith("12:00:00"))
                .Take(3)
                .Select(e => new ForecastDay
                {
                    Day       = DateTime.Parse(e!["dt_txt"]!.GetValue<string>()).DayOfWeek.ToString()[..3],
                    High      = (int)Math.Round(e!["main"]!["temp_max"]!.GetValue<double>()),
                    Low       = (int)Math.Round(e!["main"]!["temp_min"]!.GetValue<double>()),
                    Condition = e!["weather"]![0]!["main"]!.GetValue<string>(),
                })
                .ToList(),
        };

        return weather;
    }
}
