namespace GardenAI;

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

public interface IInsightGenerator
{
    string ProviderName { get; }
    string ModelName { get; }
    Task<GardenInsight> GenerateInsightAsync(
        string promptTemplate,
        WeatherForecast forecast,
        List<GardenBed> beds);
}
