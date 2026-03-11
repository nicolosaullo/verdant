using System.Net.Http.Json;
using System.Text.Json;

namespace GardenAI;

public class AnthropicInsightGenerator(string apiKey, string model = "claude-opus-4-6") : IInsightGenerator
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromMinutes(3) };
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";

    public string ProviderName => "Anthropic";
    public string ModelName => model;

    public async Task<GardenInsight> GenerateInsightAsync(string promptTemplate, WeatherForecast forecast, List<GardenBed> beds, DaylightInfo? daylight = null, SensorSnapshot? sensors = null, SensorHistory? history = null, WeatherHistory? weatherHistory = null)
    {
        var prompt = GardenInsightParser.BuildPrompt(promptTemplate, forecast, beds, daylight, sensors, history, weatherHistory);
        // Only the first bed image is sent to keep token costs down
        var image  = beds.FirstOrDefault(b => b.HasImage);

        // Build message content — image block first if available
        object content = image is not null
            ? new object[]
            {
                new
                {
                    type   = "image",
                    source = new
                    {
                        type       = "base64",
                        media_type = image.ImageMediaType,
                        data       = image.ImageData
                    }
                },
                new { type = "text", text = prompt }
            }
            : (object)prompt;

        var requestBody = new
        {
            model,
            max_tokens = 4000,
            messages   = new[] { new { role = "user", content } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = JsonContent.Create(requestBody);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var doc  = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? throw new Exception("Empty response from Anthropic API");

        return GardenInsightParser.ParseInsight(text);
    }
}
