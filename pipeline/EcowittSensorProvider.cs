using System.Globalization;
using System.Text.Json;

namespace GardenAI;

/// <summary>
/// Fetches real-time sensor data from the Ecowitt cloud API (api.ecowitt.net v3).
/// Requires ECOWITT_APPLICATION_KEY, ECOWITT_API_KEY, and ECOWITT_MAC environment variables.
/// </summary>
public static class EcowittSensorProvider
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };
    private const string BaseUrl = "https://api.ecowitt.net/api/v3/device/real_time";
    private const string HistoryUrl = "https://api.ecowitt.net/api/v3/device/history";
    private static TimeZoneInfo NzTz => GardenConfig.NzTimeZone;

    public static async Task<SensorSnapshot?> GetSensorDataAsync(
        string applicationKey, string apiKey, string mac)
    {
        try
        {
            var url = $"{BaseUrl}?application_key={applicationKey}&api_key={apiKey}" +
                      $"&mac={Uri.EscapeDataString(mac)}&call_back=all&temp_unitid=1&pressure_unitid=3&wind_speed_unitid=7&rainfall_unitid=12";
            // temp_unitid=1 → Celsius, pressure_unitid=3 → hPa, wind_speed_unitid=7 → km/h, rainfall_unitid=12 → mm

            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.GetProperty("code").GetInt32() != 0)
            {
                var msg = root.GetProperty("msg").GetString();
                Console.WriteLine($"   ⚠️  Ecowitt API error: {msg}");
                return null;
            }

            var data = root.GetProperty("data");
            return ParseSnapshot(data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Ecowitt sensor data unavailable: {SanitizeError(ex)}");
            Console.WriteLine("      Pipeline will continue without sensor data.");
            return null;
        }
    }

    private static SensorSnapshot ParseSnapshot(JsonElement data)
    {
        var outdoor = data.TryGetProperty("outdoor", out var o)
            ? new OutdoorReading(
                TemperatureC: ParseDouble(o, "temperature"),
                Humidity: ParseDouble(o, "humidity"),
                FeelsLikeC: ParseDouble(o, "feels_like"),
                DewPointC: ParseDouble(o, "dew_point"))
            : null;

        var soilChannels = new List<SoilReading>();
        for (var ch = 1; ch <= 8; ch++)
        {
            if (data.TryGetProperty($"soil_ch{ch}", out var soil))
            {
                soilChannels.Add(new SoilReading(
                    Channel: ch,
                    MoisturePct: ParseDouble(soil, "soilmoisture")));
            }
        }

        var wind = data.TryGetProperty("wind", out var w)
            ? new WindReading(
                SpeedKph: ParseDouble(w, "wind_speed"),
                GustKph: ParseDouble(w, "wind_gust"),
                DirectionDeg: ParseDouble(w, "wind_direction"))
            : null;

        var rain = data.TryGetProperty("rainfall", out var r)
            ? new RainfallReading(
                RatePerHour: ParseDouble(r, "rain_rate"),
                DailyMm: ParseDouble(r, "daily"))
            : null;

        var solar = data.TryGetProperty("solar_and_uvi", out var s)
            ? new SolarReading(
                RadiationWm2: ParseDouble(s, "solar"),
                UvIndex: ParseDouble(s, "uvi"))
            : null;

        var indoor = data.TryGetProperty("indoor", out var i)
            ? new IndoorReading(
                TemperatureC: ParseDouble(i, "temperature"),
                Humidity: ParseDouble(i, "humidity"))
            : null;

        var pressure = data.TryGetProperty("pressure", out var p)
            ? new PressureReading(
                RelativeHpa: ParseDouble(p, "relative"),
                AbsoluteHpa: ParseDouble(p, "absolute"))
            : null;

        return new SensorSnapshot(
            CapturedAt: DateTimeOffset.UtcNow,
            Outdoor: outdoor,
            SoilChannels: soilChannels,
            Wind: wind,
            Rainfall: rain,
            Solar: solar,
            Indoor: indoor,
            Pressure: pressure);
    }

    public static async Task<SensorHistory?> GetHistoryAsync(
        string applicationKey, string apiKey, string mac, int days = 7)
    {
        try
        {
            var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, NzTz);
            var end = now.ToString("yyyy-MM-dd HH:mm:ss");
            var start = now.AddDays(-days).Date.ToString("yyyy-MM-dd 00:00:00");

            var url = $"{HistoryUrl}?application_key={applicationKey}&api_key={apiKey}" +
                      $"&mac={Uri.EscapeDataString(mac)}" +
                      $"&start_date={Uri.EscapeDataString(start)}&end_date={Uri.EscapeDataString(end)}" +
                      $"&call_back=outdoor,soil_ch1&cycle_type=auto&temp_unitid=1";

            var json = await Http.GetStringAsync(url);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.GetProperty("code").GetInt32() != 0)
            {
                var msg = root.GetProperty("msg").GetString();
                Console.WriteLine($"   ⚠️  Ecowitt history API error: {msg}");
                return null;
            }

            var data = root.GetProperty("data");
            if (data.ValueKind == JsonValueKind.Array) // empty result
                return null;

            return AggregateHistory(data);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️  Ecowitt history unavailable: {SanitizeError(ex)}");
            return null;
        }
    }

    private static SensorHistory AggregateHistory(JsonElement data)
    {
        // Parse timestamped values into per-day buckets
        var tempByDay = GroupByDay(data, "outdoor", "temperature");
        var humByDay = GroupByDay(data, "outdoor", "humidity");
        var soilByDay = GroupByDay(data, "soil_ch1", "soilmoisture");

        var allDates = tempByDay.Keys
            .Union(humByDay.Keys)
            .Union(soilByDay.Keys)
            .OrderBy(d => d)
            .ToList();

        var rows = allDates.Select(date =>
        {
            var t = tempByDay.GetValueOrDefault(date);
            var h = humByDay.GetValueOrDefault(date);
            var s = soilByDay.GetValueOrDefault(date);
            return new DailyHistoryRow(
                Date: date,
                TempMinC: t is { Count: > 0 } ? t.Min() : null,
                TempMaxC: t is { Count: > 0 } ? t.Max() : null,
                TempAvgC: t is { Count: > 0 } ? Math.Round(t.Average(), 1) : null,
                HumidityMin: h is { Count: > 0 } ? h.Min() : null,
                HumidityMax: h is { Count: > 0 } ? h.Max() : null,
                SoilMoistureMin: s is { Count: > 0 } ? s.Min() : null,
                SoilMoistureMax: s is { Count: > 0 } ? s.Max() : null);
        }).ToList();

        return new SensorHistory(rows);
    }

    private static Dictionary<string, List<double>> GroupByDay(
        JsonElement data, string category, string metric)
    {
        var result = new Dictionary<string, List<double>>();

        if (!data.TryGetProperty(category, out var cat)) return result;
        if (!cat.TryGetProperty(metric, out var m)) return result;
        if (!m.TryGetProperty("list", out var list)) return result;

        foreach (var entry in list.EnumerateObject())
        {
            if (!long.TryParse(entry.Name, out var epoch)) continue;
            if (!double.TryParse(entry.Value.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var val)) continue;

            var dt = DateTimeOffset.FromUnixTimeSeconds(epoch);
            var local = TimeZoneInfo.ConvertTime(dt, NzTz);
            var day = local.ToString("dd/MM");

            if (!result.ContainsKey(day))
                result[day] = [];
            result[day].Add(val);
        }

        return result;
    }

    private static double? ParseDouble(JsonElement parent, string property)
    {
        if (!parent.TryGetProperty(property, out var prop)) return null;
        if (!prop.TryGetProperty("value", out var val)) return null;
        var s = val.GetString();
        return double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static string SanitizeError(Exception ex)
    {
        // Strip query parameters from URLs in error messages to avoid leaking API keys
        return System.Text.RegularExpressions.Regex.Replace(
            ex.Message, @"\?[^\s""']*", "?<redacted>");
    }
}
