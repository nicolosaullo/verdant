namespace GardenAI;

/// <summary>
/// Shared prompt-building and response-parsing logic used by all insight generators.
/// The same prompt is sent to every model so the outputs are directly comparable.
/// </summary>
internal static class GardenInsightParser
{
    internal static string BuildPrompt(string promptTemplate, WeatherForecast forecast, List<GardenBed> beds)
    {
        var season = DateTimeOffset.Now.Month switch
        {
            12 or 1 or 2 => "summer",
            3 or 4 or 5  => "autumn",
            6 or 7 or 8  => "winter",
            _            => "spring"
        };

        var bedsSection = beds.Count > 0
            ? "## Garden Beds\n\n" + string.Join("\n\n", beds.Select(FormatBed))
            : "";

        return promptTemplate
            .Replace("{{SEASON}}",        season)
            .Replace("{{BEDS}}",          bedsSection)
            .Replace("{{FROST_NOTE}}",    forecast.IsFrostRisk ? " ⚠️ Frost risk in next 3 days." : "")
            .Replace("{{RAIN_3DAY}}",     forecast.TotalRainNextDays(3).ToString("F1"))
            .Replace("{{FORECAST_TABLE}}", forecast.ToPromptTable());
    }

    private static string FormatBed(GardenBed bed)
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"### {bed.Name}");
        if (!string.IsNullOrEmpty(bed.Location))  lines.AppendLine($"- Location: {bed.Location}");
        if (bed.AreaSqm.HasValue)                 lines.AppendLine($"- Area: {bed.AreaSqm}m²");
        if (!string.IsNullOrEmpty(bed.Soil))      lines.AppendLine($"- Soil: {bed.Soil}");
        if (!string.IsNullOrEmpty(bed.Sun))       lines.AppendLine($"- Sun: {bed.Sun}");
        if (!string.IsNullOrEmpty(bed.Notes))
        {
            lines.AppendLine();
            lines.Append(bed.Notes);
        }
        return lines.ToString();
    }

    internal static GardenInsight ParseInsight(string rawText)
    {
        var sections = new Dictionary<string, string>();
        var currentSection = "";
        var currentContent = new System.Text.StringBuilder();

        foreach (var line in rawText.Split('\n'))
        {
            if (line.StartsWith("### "))
            {
                if (!string.IsNullOrEmpty(currentSection))
                    sections[currentSection] = currentContent.ToString().Trim();
                currentSection = line[4..].Trim();
                currentContent.Clear();
            }
            else
            {
                currentContent.AppendLine(line);
            }
        }
        if (!string.IsNullOrEmpty(currentSection))
            sections[currentSection] = currentContent.ToString().Trim();

        return new GardenInsight(
            GeneratedAt:   DateTimeOffset.Now,
            Summary:       sections.GetValueOrDefault("Summary", ""),
            Observations:  sections.GetValueOrDefault("Observations", ""),
            Actions:       sections.GetValueOrDefault("Actions", ""),
            ForecastAdvice: sections.GetValueOrDefault("Forecast Advice", ""),
            GardenersNote: sections.GetValueOrDefault("Gardener's Note", ""),
            RawResponse:   rawText
        );
    }
}
