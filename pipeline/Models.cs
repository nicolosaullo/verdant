namespace GardenAI;

public record SensorReading(
    DateTimeOffset Timestamp,
    OutdoorReading Outdoor,
    SoilReading SoilChannel1,
    SoilReading SoilChannel2,
    SoilReading? SoilChannel3 = null
);

public record OutdoorReading(
    double TemperatureCelsius,
    int HumidityPercent,
    double DewPointCelsius,
    double FeelsLikeCelsius
);

public record SoilReading(
    string ChannelName,   // e.g. "Tomatoes (North bed)"
    int MoisturePercent,
    double? BatteryVoltage = null
);

public record DailyHistory(
    DateTimeOffset Date,
    double TempMin,
    double TempMax,
    double TempAvg,
    int HumidityAvg,
    int SoilCh1Avg,
    int SoilCh2Avg
);

public record GardenInsight(
    DateTimeOffset GeneratedAt,
    SensorReading Reading,
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

public interface IInsightGenerator
{
    string ProviderName { get; }
    string ModelName { get; }
    Task<GardenInsight> GenerateInsightAsync(SensorReading current, List<DailyHistory> history);
}
