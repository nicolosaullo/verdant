namespace GardenAI;

/// <summary>
/// Shared prompt-building and response-parsing logic used by all insight generators.
/// The same prompt is sent to every model so the outputs are directly comparable.
/// </summary>
internal static class GardenInsightParser
{
    internal static string BuildPrompt(WeatherForecast forecast, List<GardenBed> beds)
    {
        var rainNext3Days = forecast.TotalRainNextDays(3);
        var frostRiskNote = forecast.IsFrostRisk ? " ⚠️ Frost risk in next 3 days." : "";

        var bedsSection = beds.Count > 0
            ? "## Garden Beds\n\n" + string.Join("\n\n", beds.Select(FormatBed))
            : "";

        // Derive current NZ season from today's month
        var season = DateTimeOffset.Now.Month switch
        {
            12 or 1 or 2 => "summer",
            3 or 4 or 5  => "autumn",
            6 or 7 or 8  => "winter",
            _            => "spring"
        };

        return $"""
            You are an expert gardening assistant with deep knowledge of vegetable growing
            in maritime climates. Analyse the data below and provide daily insights
            for a home food garden in Dunedin, New Zealand (45°S, oceanic climate,
            currently {season}).

            {bedsSection}

            ## 7-Day Weather Forecast (Open-Meteo){frostRiskNote}
            Rain expected in next 3 days: {rainNext3Days:F1}mm
            {forecast.ToPromptTable()}

            ## Your task
            Respond in the following EXACT format with these section headers:

            ### Summary
            A 2-3 sentence plain-English overview of today's garden conditions, written
            in a warm, conversational tone suitable for a public blog post. Reference
            the specific plants by name.

            ### Observations
            3-5 bullet points of specific, data-driven observations about trends,
            anomalies, or noteworthy patterns based on the forecast data.

            ### Actions
            Concrete, prioritised actions the gardener should take today or in the
            next 24 hours. Factor in the weather forecast — if significant rain is
            coming soon, avoid watering. If a frost is forecast, recommend protection.
            Tailor advice to the specific plants in the bed.

            ### Forecast Advice
            1-2 sentences on what to watch for over the next few days, using the
            weather forecast data to give specific, actionable guidance.

            ### Gardener's Note
            A short (1-2 sentence) reflective or encouraging note that acknowledges
            the journey of learning through data. Keep it human and grounded.
            """;
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
