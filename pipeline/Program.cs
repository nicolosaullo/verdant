using GardenAI;

// ─── Configuration ────────────────────────────────────────────────────────────
// Keys are optional — providers with no key are silently skipped.
// At least one key must be present or the pipeline exits early.
var anthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
var openAiApiKey    = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

var ecowittAppKey   = Environment.GetEnvironmentVariable("ECOWITT_APPLICATION_KEY");
var ecowittApiKey   = Environment.GetEnvironmentVariable("ECOWITT_API_KEY");
var ecowittMac      = Environment.GetEnvironmentVariable("ECOWITT_MAC");

var outputDirectory = Environment.GetEnvironmentVariable("POSTS_OUTPUT_DIR")
    ?? "./output/posts";

var bedsDirectory = Environment.GetEnvironmentVariable("BEDS_CONFIG_DIR")
    ?? "./garden/beds";

var promptFile = Environment.GetEnvironmentVariable("PROMPT_FILE")
    ?? "./garden/prompt.md";

// ─── Pipeline ─────────────────────────────────────────────────────────────────
Console.WriteLine("🌱 Garden AI Pipeline starting...\n");

// Step 1: Load prompt template, bed config, and weather forecast
Console.WriteLine("📡 Loading prompt, bed config, and weather forecast...");
if (!File.Exists(promptFile))
{
    Console.Error.WriteLine($"❌ Prompt file not found: {promptFile}");
    return 1;
}
var promptTemplate = await File.ReadAllTextAsync(promptFile);

var bedsTask           = Task.Run(() => GardenBedLoader.LoadAll(bedsDirectory));
var forecastTask       = WeatherForecastProvider.GetForecastAsync();
var daylightTask       = DaylightProvider.GetDaylightAsync();
var weatherHistoryTask = WeatherHistoryProvider.GetHistoryAsync();
var hasEcowitt = ecowittAppKey is not null && ecowittApiKey is not null && ecowittMac is not null;
var sensorsTask = hasEcowitt
    ? EcowittSensorProvider.GetSensorDataAsync(ecowittAppKey!, ecowittApiKey!, ecowittMac!)
    : Task.FromResult<SensorSnapshot?>(null);
var historyTask = hasEcowitt
    ? EcowittSensorProvider.GetHistoryAsync(ecowittAppKey!, ecowittApiKey!, ecowittMac!)
    : Task.FromResult<SensorHistory?>(null);

await Task.WhenAll(bedsTask, forecastTask, daylightTask, weatherHistoryTask, sensorsTask, historyTask);

var beds           = bedsTask.Result;
var forecast       = forecastTask.Result;
var daylight       = daylightTask.Result;
var weatherHistory = weatherHistoryTask.Result;
var sensors        = sensorsTask.Result;
var history        = historyTask.Result;

if (beds.Count > 0)
    Console.WriteLine($"   Beds loaded: {string.Join(", ", beds.Select(b => $"{b.Name}" + (b.HasImage ? " 📷" : "")))}");
else
    Console.WriteLine("   No bed config found — running without bed context.");

Console.WriteLine($"   Forecast: {forecast.TotalRainNextDays(3):F1}mm rain in next 3 days" +
                  (forecast.IsFrostRisk ? ", ⚠️ frost risk" : "") +
                  (daylight is not null ? $", day length: {daylight.DayLengthHours:F1}h" : "") + "\n");
if (weatherHistory.Days.Count > 0)
    Console.WriteLine($"   Weather history: {weatherHistory.Days.Count} day(s) of actual weather data");

if (sensors is not null)
{
    Console.WriteLine($"   Sensors: outdoor {sensors.Outdoor?.TemperatureC:F1}°C, " +
                      $"humidity {sensors.Outdoor?.Humidity:F0}%, " +
                      $"{sensors.SoilChannels.Count} soil channel(s)");
    if (history is { Days.Count: > 0 })
        Console.WriteLine($"   History: {history.Days.Count} day(s) of sensor data");
}
else if (!hasEcowitt)
    Console.WriteLine("   ⚠️  Ecowitt keys not set — running without sensor data.");


// Step 2: Build the generator list from whichever keys are present
//   ► Add or remove entries here to change which models are compared
var generators = new List<IInsightGenerator>();

if (anthropicApiKey is not null)
{
    generators.Add(new AnthropicInsightGenerator(anthropicApiKey, "claude-opus-4-6"));
    generators.Add(new AnthropicInsightGenerator(anthropicApiKey, "claude-sonnet-4-6"));
}
else
    Console.WriteLine("⚠️  ANTHROPIC_API_KEY not set — skipping Anthropic models.\n");

if (openAiApiKey is not null)
{
    generators.Add(new OpenAIInsightGenerator(openAiApiKey, "gpt-5.4"));
}
else
    Console.WriteLine("⚠️  OPENAI_API_KEY not set — skipping OpenAI models.\n");

if (generators.Count == 0)
{
    Console.Error.WriteLine("❌ No API keys found. Set at least one of ANTHROPIC_API_KEY or OPENAI_API_KEY.");
    return 1;
}

Console.WriteLine($"🤖 Querying {generators.Count} models in parallel...");
foreach (var g in generators)
    Console.WriteLine($"   → {g.ProviderName} / {g.ModelName}");
Console.WriteLine();

var tasks = generators.Select(async g =>
{
    try
    {
        var insight = await g.GenerateInsightAsync(promptTemplate, forecast, beds, daylight, sensors, history, weatherHistory);
        Console.WriteLine($"   ✅ {g.ProviderName}/{g.ModelName} done");
        return new ProviderInsight(g.ProviderName, g.ModelName, insight);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"   ⚠️  {g.ProviderName}/{g.ModelName} failed: {ex.Message}");
        return new ProviderInsight(g.ProviderName, g.ModelName, null, ex.Message);
    }
});

var results   = await Task.WhenAll(tasks);
var succeeded = results.Count(r => r.Succeeded);
Console.WriteLine($"\n   {succeeded}/{generators.Count} models succeeded.\n");

if (succeeded == 0)
{
    Console.Error.WriteLine("❌ All models failed — no post will be generated.");
    return 1;
}

// Step 3: Publish comparison post
Console.WriteLine("📄 Publishing comparison post...");
await BlogPostPublisher.SavePostAsync(forecast, results, outputDirectory, daylight, sensors);

Console.WriteLine("\n✨ Pipeline complete!");
Console.WriteLine($"   Post saved to: {Path.GetFullPath(outputDirectory)}");
return 0;
