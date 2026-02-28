using System.Text.Json;
using System.Text.Json.Serialization;

namespace GardenAI;

/// <summary>
/// Saves a daily snapshot of raw sensor and weather data to garden/data/YYYY-MM-DD.json.
/// </summary>
public static class DataLogger
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task SaveAsync(
        SensorReading current,
        List<DailyHistory> history,
        WeatherForecast forecast,
        string dataDirectory)
    {
        Directory.CreateDirectory(dataDirectory);

        var snapshot = new DailySnapshot(
            CapturedAt:  current.Timestamp,
            Current:     current,
            History:     history,
            Forecast:    forecast.Days
        );

        var filename = Path.Combine(dataDirectory, $"{current.Timestamp:yyyy-MM-dd}.json");
        var json     = JsonSerializer.Serialize(snapshot, Options);
        await File.WriteAllTextAsync(filename, json);
        Console.WriteLine($"   Raw data saved: {filename}");
    }
}

public record DailySnapshot(
    DateTimeOffset      CapturedAt,
    SensorReading       Current,
    List<DailyHistory>  History,
    List<ForecastDay>   Forecast
);
