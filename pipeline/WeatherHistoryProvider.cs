using System.Text.Json;

namespace GardenAI;

/// <summary>
/// Fetches the past 7 days of actual weather from Open-Meteo Historical Weather API (free, no key).
/// Port Chalmers coordinates: -45.8175, 170.6275
/// Note: the archive API has a ~2-day lag, so end_date is set to 2 days ago.
/// </summary>
public static class WeatherHistoryProvider
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };

    public static async Task<WeatherHistory> GetHistoryAsync()
    {
        try
        {
            var nzNow   = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, GardenConfig.NzTimeZone);
            var endDate   = nzNow.Date.AddDays(-2); // archive has ~2-day lag
            var startDate = endDate.AddDays(-6);    // 7 days total

            var url =
                "https://archive-api.open-meteo.com/v1/archive" +
                $"?latitude=-45.8175&longitude=170.6275" +
                $"&start_date={startDate:yyyy-MM-dd}&end_date={endDate:yyyy-MM-dd}" +
                "&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,windspeed_10m_max,et0_fao_evapotranspiration,shortwave_radiation_sum" +
                "&timezone=Pacific%2FAuckland";

            var json  = await _http.GetStringAsync(url);
            var doc   = JsonDocument.Parse(json);
            var daily = doc.RootElement.GetProperty("daily");

            var dates     = daily.GetProperty("time").EnumerateArray().Select(e => e.GetString()!).ToArray();
            var tempMax   = daily.GetProperty("temperature_2m_max").EnumerateArray().Select(NullableDouble).ToArray();
            var tempMin   = daily.GetProperty("temperature_2m_min").EnumerateArray().Select(NullableDouble).ToArray();
            var rain      = daily.GetProperty("precipitation_sum").EnumerateArray().Select(NullableDouble).ToArray();
            var wind      = daily.GetProperty("windspeed_10m_max").EnumerateArray().Select(NullableDouble).ToArray();
            var et0       = daily.GetProperty("et0_fao_evapotranspiration").EnumerateArray().Select(NullableDouble).ToArray();
            var radiation = daily.GetProperty("shortwave_radiation_sum").EnumerateArray().Select(NullableDouble).ToArray();

            var days = dates.Select((date, i) => new HistoryDay(
                Date:                   DateOnly.ParseExact(date, "yyyy-MM-dd").ToString("dd/MM"),
                TempMaxCelsius:         tempMax[i],
                TempMinCelsius:         tempMin[i],
                PrecipitationMm:        rain[i],
                WindspeedKph:           wind[i],
                Et0Mm:                  et0[i],
                ShortwaveRadiationMjm2: radiation[i]
            )).ToList();

            return new WeatherHistory(days);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Weather history unavailable: {ex.Message}");
            Console.WriteLine("      Pipeline will continue without historical weather data.");
            return new WeatherHistory([]);
        }
    }

    private static double? NullableDouble(JsonElement e) =>
        e.ValueKind == JsonValueKind.Null ? null : e.GetDouble();
}

public record WeatherHistory(List<HistoryDay> Days)
{
    public string ToPromptTable() =>
        "  Date       | Max    | Min    | Rain   | Wind     | ET₀    | Radiation\n" +
        "  -----------|--------|--------|--------|----------|--------|----------\n" +
        string.Join("\n", Days.Select(d =>
            $"  {d.Date} | {Fmt(d.TempMaxCelsius),5}°C | {Fmt(d.TempMinCelsius),5}°C | " +
            $"{FmtMm(d.PrecipitationMm),5} | {FmtKph(d.WindspeedKph),7} | {FmtMm(d.Et0Mm),5} | {FmtMj(d.ShortwaveRadiationMjm2)}"));

    private static string Fmt(double? v)    => v.HasValue ? v.Value.ToString("F1") : " n/a";
    private static string FmtMm(double? v)  => v.HasValue ? $"{v.Value:F1}mm" : "  n/a";
    private static string FmtKph(double? v) => v.HasValue ? $"{v.Value:F1}kph" : "    n/a";
    private static string FmtMj(double? v)  => v.HasValue ? $"{v.Value:F1}MJ/m²" : "n/a";
}

public record HistoryDay(
    string  Date,
    double? TempMaxCelsius,
    double? TempMinCelsius,
    double? PrecipitationMm,
    double? WindspeedKph,
    double? Et0Mm,
    double? ShortwaveRadiationMjm2
);
