namespace GardenAI;

/// <summary>Shared timezone for all NZ date/time conversions (works on both Windows and Linux).</summary>
public static class GardenConfig
{
    public static readonly TimeZoneInfo NzTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
}

public record GardenInsight(
    DateTimeOffset GeneratedAt,
    string Summary,
    string Observations,
    string Actions,
    string ForecastAdvice,
    string GardenersNote,
    string RawResponse
);

/// <summary>
/// Wraps a single model's result, including any error if the API call failed.
/// </summary>
public record ProviderInsight(
    string ProviderName,
    string ModelName,
    GardenInsight? Insight,
    string? Error = null
)
{
    public bool Succeeded => Insight is not null;
}

/// <summary>
/// Configuration and planting details for a physical garden bed,
/// loaded from a Markdown file in garden/beds/.
/// </summary>
public record GardenBed(
    List<int> Channels,        // sensor channels that monitor this bed (reserved for future use)
    string Name,
    string Location,
    double? AreaSqm,
    string Soil,
    string Sun,
    string Notes,              // markdown body: what's planted, care notes
    string? ImageData,         // base64-encoded image, if present alongside the .md file
    string? ImageMediaType     // e.g. "image/jpeg"
)
{
    public bool HasImage => ImageData is not null;
}

public record DaylightInfo(
    DateTimeOffset SunriseUtc,
    DateTimeOffset SunsetUtc,
    double DayLengthHours
)
{
    public string SunriseLocal => TimeZoneInfo.ConvertTime(SunriseUtc, GardenConfig.NzTimeZone).ToString("h:mm tt");
    public string SunsetLocal  => TimeZoneInfo.ConvertTime(SunsetUtc,  GardenConfig.NzTimeZone).ToString("h:mm tt");
};

// ─── Ecowitt sensor records ──────────────────────────────────────────────────

public record SensorSnapshot(
    DateTimeOffset CapturedAt,
    OutdoorReading? Outdoor,
    List<SoilReading> SoilChannels,
    WindReading? Wind,
    RainfallReading? Rainfall,
    SolarReading? Solar,
    IndoorReading? Indoor,
    PressureReading? Pressure);

public record DailyHistoryRow(
    string Date,
    double? TempMinC,
    double? TempMaxC,
    double? TempAvgC,
    double? HumidityMin,
    double? HumidityMax,
    double? SoilMoistureMin,
    double? SoilMoistureMax);

public record SensorHistory(List<DailyHistoryRow> Days);

public record OutdoorReading(double? TemperatureC, double? Humidity, double? FeelsLikeC, double? DewPointC);
public record SoilReading(int Channel, double? MoisturePct);
public record WindReading(double? SpeedKph, double? GustKph, double? DirectionDeg);
public record RainfallReading(double? RatePerHour, double? DailyMm);
public record SolarReading(double? RadiationWm2, double? UvIndex);
public record IndoorReading(double? TemperatureC, double? Humidity);
public record PressureReading(double? RelativeHpa, double? AbsoluteHpa);

// ─── Insight generator interface ─────────────────────────────────────────────

public interface IInsightGenerator
{
    string ProviderName { get; }
    string ModelName { get; }
    Task<GardenInsight> GenerateInsightAsync(
        string promptTemplate,
        WeatherForecast forecast,
        List<GardenBed> beds,
        DaylightInfo? daylight = null,
        SensorSnapshot? sensors = null,
        SensorHistory? history = null);
}
