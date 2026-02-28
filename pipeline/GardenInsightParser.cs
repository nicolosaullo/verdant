namespace GardenAI;

/// <summary>
/// Shared prompt-building and response-parsing logic used by all insight generators.
/// The same prompt is sent to every model so the outputs are directly comparable.
/// </summary>
internal static class GardenInsightParser
{
    internal static string BuildPrompt(
        SensorReading current,
        List<DailyHistory> history,
        WeatherForecast forecast)
    {
        var historyTable = string.Join("\n", history.Select(h =>
            $"  {h.Date:dd MMM}: Temp {h.TempMin}–{h.TempMax}°C (avg {h.TempAvg}°C), " +
            $"Humidity {h.HumidityAvg}%, " +
            $"{current.SoilChannel1.ChannelName} soil {h.SoilCh1Avg}%, " +
            $"{current.SoilChannel2.ChannelName} soil {h.SoilCh2Avg}%"
        ));

        var rainNext3Days  = forecast.TotalRainNextDays(3);
        var frostRiskNote  = forecast.IsFrostRisk ? " ⚠️ Frost risk in next 3 days." : "";

        return $"""
            You are an expert gardening assistant with deep knowledge of vegetable growing
            in maritime climates. Analyse the sensor data below and provide daily insights
            for a home food garden in Dunedin, New Zealand (45°S, oceanic climate,
            currently late summer / early autumn).

            ## Current Readings ({current.Timestamp:dd MMM yyyy, h:mm tt})
            - Outdoor temp: {current.Outdoor.TemperatureCelsius}°C
              (feels like {current.Outdoor.FeelsLikeCelsius}°C, dew point {current.Outdoor.DewPointCelsius}°C)
            - Outdoor humidity: {current.Outdoor.HumidityPercent}%
            - Soil moisture — {current.SoilChannel1.ChannelName}: {current.SoilChannel1.MoisturePercent}%
            - Soil moisture — {current.SoilChannel2.ChannelName}: {current.SoilChannel2.MoisturePercent}%

            ## 7-Day Sensor History
            {historyTable}

            ## 7-Day Weather Forecast (Open-Meteo){frostRiskNote}
            Rain expected in next 3 days: {rainNext3Days:F1}mm
            {forecast.ToPromptTable()}

            ## Your task
            Respond in the following EXACT format with these section headers:

            ### Summary
            A 2-3 sentence plain-English overview of today's garden conditions, written
            in a warm, conversational tone suitable for a public blog post.

            ### Observations
            3-5 bullet points of specific, data-driven observations about trends,
            anomalies, or noteworthy patterns in the sensor data.

            ### Actions
            Concrete, prioritised actions the gardener should take today or in the
            next 24 hours. Factor in the weather forecast — if significant rain is
            coming soon, avoid watering. If a frost is forecast, recommend protection.
            Be specific (e.g. "Hold off watering the tomato bed — 12mm of rain is
            forecast tomorrow").

            ### Forecast Advice
            1-2 sentences on what to watch for over the next few days, using the
            weather forecast data to give specific, actionable guidance.

            ### Gardener's Note
            A short (1-2 sentence) reflective or encouraging note that acknowledges
            the journey of learning through data. Keep it human and grounded.
            """;
    }

    internal static GardenInsight ParseInsight(string rawText, SensorReading reading)
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
            GeneratedAt: reading.Timestamp,
            Reading: reading,
            Summary: sections.GetValueOrDefault("Summary", ""),
            Observations: sections.GetValueOrDefault("Observations", ""),
            Actions: sections.GetValueOrDefault("Actions", ""),
            ForecastAdvice: sections.GetValueOrDefault("Forecast Advice", ""),
            GardenersNote: sections.GetValueOrDefault("Gardener's Note", ""),
            RawResponse: rawText
        );
    }
}
