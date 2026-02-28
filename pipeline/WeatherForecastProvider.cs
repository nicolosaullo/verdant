using System.Text.Json;

namespace GardenAI;

/// <summary>
/// Fetches a 7-day hourly weather forecast from Open-Meteo (free, no API key).
/// Port Chalmers coordinates: -45.8175, 170.6275
/// </summary>
public static class WeatherForecastProvider
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private const string Url =
        "https://api.open-meteo.com/v1/forecast" +
        "?latitude=-45.8175&longitude=170.6275" +
        "&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,precipitation_probability_max,windspeed_10m_max,uv_index_max" +
        "&timezone=Pacific%2FAuckland" +
        "&forecast_days=7";

    public static async Task<WeatherForecast> GetForecastAsync()
    {
        try
        {
            var json  = await _http.GetStringAsync(Url);
            var doc   = JsonDocument.Parse(json);
            var daily = doc.RootElement.GetProperty("daily");

            var dates    = daily.GetProperty("time").EnumerateArray().Select(e => e.GetString()!).ToArray();
            var tempMax  = daily.GetProperty("temperature_2m_max").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            var tempMin  = daily.GetProperty("temperature_2m_min").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            var rain     = daily.GetProperty("precipitation_sum").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            var rainProb = daily.GetProperty("precipitation_probability_max").EnumerateArray().Select(e => e.GetInt32()).ToArray();
            var wind     = daily.GetProperty("windspeed_10m_max").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            var uv       = daily.GetProperty("uv_index_max").EnumerateArray().Select(e => e.GetDouble()).ToArray();

            var days = dates.Select((date, i) => new ForecastDay(
                Date:            date,
                TempMaxCelsius:  tempMax[i],
                TempMinCelsius:  tempMin[i],
                PrecipitationMm: rain[i],
                PrecipProbPct:   rainProb[i],
                WindspeedKph:    wind[i],
                UvIndex:         uv[i]
            )).ToList();

            return new WeatherForecast(days);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Weather forecast unavailable: {ex.Message}");
            Console.WriteLine("      Pipeline will continue without forecast data.");
            return new WeatherForecast([]);
        }
    }
}

public record WeatherForecast(List<ForecastDay> Days)
{
    public double TotalRainNextDays(int days) =>
        Days.Take(days).Sum(d => d.PrecipitationMm);

    public bool IsFrostRisk =>
        Days.Take(3).Any(d => d.TempMinCelsius <= 2.0);

    /// <summary>
    /// Formats the forecast as a compact table for the AI prompt.
    /// </summary>
    public string ToPromptTable() =>
        "  Date       | Max  | Min  | Rain  | Rain% | Wind    | UV\n" +
        "  -----------|------|------|-------|-------|---------|----\n" +
        string.Join("\n", Days.Select(d =>
            $"  {d.Date} | {d.TempMaxCelsius,4:F1}°C | {d.TempMinCelsius,4:F1}°C | " +
            $"{d.PrecipitationMm,4:F1}mm | {d.PrecipProbPct,4}%  | " +
            $"{d.WindspeedKph,5:F1}kph | {d.UvIndex:F1}"));
}

public record ForecastDay(
    string Date,
    double TempMaxCelsius,
    double TempMinCelsius,
    double PrecipitationMm,
    int    PrecipProbPct,
    double WindspeedKph,
    double UvIndex
);
