using System.Text.Json;

namespace GardenAI;

/// <summary>
/// Fetches sunrise, sunset, and day length from the free sunrise-sunset.org API.
/// Port Chalmers coordinates: -45.8175, 170.6275
/// </summary>
public static class DaylightProvider
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };

    private const string Url =
        "https://api.sunrise-sunset.org/json?lat=-45.8175&lng=170.6275&formatted=0";

    public static async Task<DaylightInfo?> GetDaylightAsync()
    {
        try
        {
            var json   = await _http.GetStringAsync(Url);
            var doc    = JsonDocument.Parse(json);
            var status = doc.RootElement.GetProperty("status").GetString();
            if (status != "OK")
                throw new Exception($"sunrise-sunset.org returned status: {status}");

            var results    = doc.RootElement.GetProperty("results");
            var sunrise    = results.GetProperty("sunrise").GetString()!;
            var sunset     = results.GetProperty("sunset").GetString()!;
            var daySeconds = results.GetProperty("day_length").GetInt32();

            return new DaylightInfo(
                SunriseUtc:     DateTimeOffset.Parse(sunrise),
                SunsetUtc:      DateTimeOffset.Parse(sunset),
                DayLengthHours: daySeconds / 3600.0
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Daylight data unavailable: {ex.Message}");
            Console.WriteLine("      Pipeline will continue without daylight data.");
            return null;
        }
    }
}
