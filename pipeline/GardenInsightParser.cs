namespace GardenAI;

/// <summary>
/// Shared prompt-building and response-parsing logic used by all insight generators.
/// The same prompt is sent to every model so the outputs are directly comparable.
/// </summary>
internal static class GardenInsightParser
{
    private static TimeZoneInfo NzTz => GardenConfig.NzTimeZone;

    internal static string BuildPrompt(string promptTemplate, WeatherForecast forecast, List<GardenBed> beds, DaylightInfo? daylight = null, SensorSnapshot? sensors = null, SensorHistory? history = null, WeatherHistory? weatherHistory = null)
    {
        var season = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, NzTz).Month switch
        {
            12 or 1 or 2 => "summer",
            3 or 4 or 5  => "autumn",
            6 or 7 or 8  => "winter",
            _            => "spring"
        };

        var bedsSection = beds.Count > 0
            ? "## Garden Beds\n\n" + string.Join("\n\n", beds.Select(b => FormatBed(b, sensors)))
            : "";

        var daylightSection = daylight is not null
            ? $"Day length: {daylight.DayLengthHours:F2} hours " +
              $"(sunrise {daylight.SunriseLocal}, sunset {daylight.SunsetLocal} NZST/NZDT). " +
              $"Season: {season} at 45°S — watch for bolting in brassicas/lettuce if days are long."
            : $"Season: {season} at 45°S.";

        var today = forecast.Days.FirstOrDefault();
        var et0Section = today is not null
            ? $"Today's ET₀: {today.Et0Mm:F1}mm, solar radiation: {today.ShortwaveRadiationMjm2:F1} MJ/m²."
            : "";

        var todayDate = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, NzTz).ToString("dd/MM/yyyy");
        var sensorSection = sensors is not null ? FormatSensors(sensors) : "No live sensor data available.";
        var historySection = history is { Days.Count: > 0 } ? FormatHistory(history) : "No historical sensor data available yet.";
        var weatherHistorySection = weatherHistory is { Days.Count: > 0 } ? weatherHistory.ToPromptTable() : "No historical weather data available yet.";

        return promptTemplate
            .Replace("{{TODAY}}",           todayDate)
            .Replace("{{SEASON}}",          season)
            .Replace("{{BEDS}}",            bedsSection)
            .Replace("{{FROST_NOTE}}",      forecast.IsFrostRisk ? " ⚠️ Frost risk in next 3 days." : "")
            .Replace("{{RAIN_3DAY}}",       forecast.TotalRainNextDays(3).ToString("F1"))
            .Replace("{{FORECAST_TABLE}}",  forecast.ToPromptTable())
            .Replace("{{DAYLIGHT}}",        daylightSection)
            .Replace("{{ET0_TODAY}}",       et0Section)
            .Replace("{{SENSORS}}",         sensorSection)
            .Replace("{{SENSOR_HISTORY}}",  historySection)
            .Replace("{{WEATHER_HISTORY}}", weatherHistorySection);
    }

    private static string FormatBed(GardenBed bed, SensorSnapshot? sensors)
    {
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"### {bed.Name}");
        if (!string.IsNullOrEmpty(bed.Location))  lines.AppendLine($"- Location: {bed.Location}");
        if (bed.AreaSqm.HasValue)                 lines.AppendLine($"- Area: {bed.AreaSqm}m²");
        if (!string.IsNullOrEmpty(bed.Soil))      lines.AppendLine($"- Soil: {bed.Soil}");
        if (!string.IsNullOrEmpty(bed.Sun))       lines.AppendLine($"- Sun: {bed.Sun}");

        // Map soil channels to this bed
        if (sensors is not null && bed.Channels.Count > 0)
        {
            foreach (var ch in bed.Channels)
            {
                var reading = sensors.SoilChannels.FirstOrDefault(s => s.Channel == ch);
                if (reading?.MoisturePct is { } pct)
                    lines.AppendLine($"- Soil moisture: {pct:F0}% (sensor ch{ch})");
            }
        }

        if (!string.IsNullOrEmpty(bed.Notes))
        {
            lines.AppendLine();
            lines.Append(bed.Notes);
        }
        return lines.ToString();
    }

    private static string FormatSensors(SensorSnapshot s)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("  Metric              | Value");
        sb.AppendLine("  --------------------|------");

        if (s.Outdoor is { } o)
        {
            sb.AppendLine($"  Temperature         | {V(o.TemperatureC, "°C")}");
            sb.AppendLine($"  Feels like          | {V(o.FeelsLikeC, "°C")}");
            sb.AppendLine($"  Humidity            | {V(o.Humidity, "%", "F0")}");
            sb.AppendLine($"  Dew point           | {V(o.DewPointC, "°C")}");
        }

        foreach (var soil in s.SoilChannels)
            sb.AppendLine($"  Soil moisture (ch{soil.Channel}) | {V(soil.MoisturePct, "%", "F0")}");

        if (s.Wind is { } w)
        {
            sb.AppendLine($"  Wind speed          | {V(w.SpeedKph, " km/h")}");
            sb.AppendLine($"  Wind gust           | {V(w.GustKph, " km/h")}");
            sb.AppendLine($"  Wind direction      | {V(w.DirectionDeg, "°", "F0")}");
        }

        if (s.Rainfall is { } r)
        {
            sb.AppendLine($"  Rain today          | {V(r.DailyMm, "mm")}");
            sb.AppendLine($"  Rain rate           | {V(r.RatePerHour, "mm/hr")}");
        }

        if (s.Solar is { } sol)
        {
            sb.AppendLine($"  Solar radiation     | {V(sol.RadiationWm2, " W/m²", "F0")}");
            sb.AppendLine($"  UV index            | {V(sol.UvIndex, "", "F0")}");
        }

        if (s.Pressure is { } p)
            sb.AppendLine($"  Pressure            | {V(p.RelativeHpa, " hPa")}");

        static string V(double? v, string unit, string fmt = "F1") =>
            v.HasValue ? $"{v.Value.ToString(fmt)}{unit}" : "n/a";

        return sb.ToString().TrimEnd();
    }

    private static string FormatHistory(SensorHistory history)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("  Date       | Temp Min | Temp Max | Temp Avg | Humidity   | Soil Moisture");
        sb.AppendLine("  -----------|----------|----------|----------|------------|-------------");
        foreach (var d in history.Days)
        {
            sb.AppendLine($"  {d.Date} | {Fmt(d.TempMinC)}°C   | {Fmt(d.TempMaxC)}°C   | {Fmt(d.TempAvgC)}°C   | {Fmt(d.HumidityMin)}-{Fmt(d.HumidityMax)}% | {Fmt(d.SoilMoistureMin)}-{Fmt(d.SoilMoistureMax)}%");
        }
        return sb.ToString().TrimEnd();

        static string Fmt(double? v) => v.HasValue ? v.Value.ToString("F1").PadLeft(5) : "  n/a";
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
            GeneratedAt:   TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, NzTz),
            Summary:       sections.GetValueOrDefault("Summary", ""),
            Observations:  sections.GetValueOrDefault("Observations", ""),
            Actions:       sections.GetValueOrDefault("Actions", ""),
            VarietyWatch:  sections.GetValueOrDefault("Variety Watch", ""),
            ForecastAdvice: sections.GetValueOrDefault("Forecast Advice", ""),
            Horticulture:  sections.GetValueOrDefault("Horticulture", ""),
            GardenersNote: sections.GetValueOrDefault("Gardener's Note", ""),
            RawResponse:   rawText
        );
    }
}
