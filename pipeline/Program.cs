using GardenAI;

// ─── Configuration ────────────────────────────────────────────────────────────
// Keys are optional — providers with no key are silently skipped.
// At least one key must be present or the pipeline exits early.
var anthropicApiKey = Environment.GetEnvironmentVariable("ANTHROPIC_API_KEY");
var openAiApiKey    = Environment.GetEnvironmentVariable("OPENAI_API_KEY");

var outputDirectory = Environment.GetEnvironmentVariable("POSTS_OUTPUT_DIR")
    ?? "./output/posts";

// ─── Pipeline ─────────────────────────────────────────────────────────────────
Console.WriteLine("🌱 Garden AI Pipeline starting...\n");

// Step 1: Sensor data + weather forecast (fetched in parallel)
Console.WriteLine("📡 Fetching sensor data (mock) and weather forecast...");
var sensorTask   = Task.Run(() => (MockSensorProvider.GetCurrentReading(), MockSensorProvider.GetSevenDayHistory()));
var forecastTask = WeatherForecastProvider.GetForecastAsync();

await Task.WhenAll(sensorTask, forecastTask);

var (currentReading, history) = sensorTask.Result;
var forecast = forecastTask.Result;

Console.WriteLine($"   Outdoor temp:     {currentReading.Outdoor.TemperatureCelsius}°C");
Console.WriteLine($"   Outdoor humidity: {currentReading.Outdoor.HumidityPercent}%");
Console.WriteLine($"   {currentReading.SoilChannel1.ChannelName}: {currentReading.SoilChannel1.MoisturePercent}%");
Console.WriteLine($"   {currentReading.SoilChannel2.ChannelName}: {currentReading.SoilChannel2.MoisturePercent}%");
Console.WriteLine($"   Forecast: {forecast.TotalRainNextDays(3):F1}mm rain in next 3 days" +
                  (forecast.IsFrostRisk ? ", ⚠️ frost risk" : "") + "\n");

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
    generators.Add(new OpenAIInsightGenerator(openAiApiKey, "gpt-4o"));
    generators.Add(new OpenAIInsightGenerator(openAiApiKey, "gpt-4o-mini"));
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
        var insight = await g.GenerateInsightAsync(currentReading, history, forecast);
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
await BlogPostPublisher.SavePostAsync(currentReading, forecast, results, outputDirectory);

Console.WriteLine("\n✨ Pipeline complete!");
Console.WriteLine($"   Post saved to: {Path.GetFullPath(outputDirectory)}");
return 0;
