using System.Net.Http.Json;
using System.Text.Json;

namespace GardenAI;

public class OpenAIInsightGenerator(string apiKey, string model = "gpt-4o") : IInsightGenerator
{
    private static readonly HttpClient _http = new();
    private const string ApiUrl = "https://api.openai.com/v1/chat/completions";

    public string ProviderName => "OpenAI";
    public string ModelName => model;

    public async Task<GardenInsight> GenerateInsightAsync(SensorReading current, List<DailyHistory> history)
    {
        var requestBody = new
        {
            model,
            max_tokens = 1500,
            messages = new[] { new { role = "user", content = GardenInsightParser.BuildPrompt(current, history) } }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
        request.Headers.Add("Authorization", $"Bearer {apiKey}");
        request.Content = JsonContent.Create(requestBody);

        var response = await _http.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var text = doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? throw new Exception("Empty response from OpenAI API");

        return GardenInsightParser.ParseInsight(text, current);
    }
}
