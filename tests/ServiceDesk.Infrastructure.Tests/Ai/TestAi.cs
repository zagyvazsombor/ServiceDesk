using System.Text.Json;
using ServiceDesk.Application.Common.Ai;
using ServiceDesk.Infrastructure.Ai;

namespace ServiceDesk.Infrastructure.Tests.Ai;

// The Ai section of appsettings.json, a suggestion input with two active categories and model answers.
internal static class TestAi
{
    public const string Model = "gemma3:4b";
    public const string Title = "Nem megy a wifi";
    public const string Description = "Reggel óta nem csatlakozik a laptop a vezeték nélküli hálózathoz.";

    public static AiOptions OllamaOptions() => new()
    {
        Provider = AiProvider.Ollama,
        TimeoutSeconds = 20,
        PromptVersion = "v1",
        Ollama = new OllamaOptions { Endpoint = "http://localhost:11434", Model = Model },
    };

    public static CategorySuggestionInput Input(string title = Title, string description = Description) => new(
        title,
        description,
        [
            new SuggestionCategory("HARDWARE", "Hardver", "Számítógépek, perifériák és egyéb eszközök hibái."),
            new SuggestionCategory("NETWORK", "Hálózat", "Hálózati kapcsolat, Wi-Fi és VPN hibái."),
        ]);

    // The JSON object the prompt asks for (camelCase, as the requested JSON schema).
    public static string Answer(string categoryCode, double confidence, string reasoning) =>
        JsonSerializer.Serialize(new { categoryCode, confidence, reasoning });
}
