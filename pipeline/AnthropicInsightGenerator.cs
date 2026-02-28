using System.Net.Http.Json;
using System.Text.Json;

namespace GardenAI;

public class AnthropicInsightGenerator(string apiKey, string model = "claude-opus-4-6") : IInsightGenerator
{
    private static readonly HttpClient _http = new();
    private const string ApiUrl = "https://api.anthropic.com/v1/messages";

    public string ProviderName => "Anthropic";
    public string ModelName => model;

    public async Task<GardenInsight> GenerateInsightAsync(SensorReading current, List<DailyHistory> history, WeatherForecast forecast)
    {
        var requestBody = new
        {
            model,
            max_tokens = 1500,
            messages = new[] { new { role = "user", content = GardenInsightParser.BuildPrompt(current, history, forecast) } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Headers.Add("x-api-key", apiKey);
        request.Headers.Add("anthropic-version", "2023-06-01");
        request.Content = JsonContent.Create(requestBody);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = doc.RootElement
            .GetProperty("content")[0]
            .GetProperty("text")
            .GetString() ?? throw new Exception("Empty response from Anthropic API");

        return GardenInsightParser.ParseInsight(text, current);
    }
}
