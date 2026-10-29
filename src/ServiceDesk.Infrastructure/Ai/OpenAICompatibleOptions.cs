namespace ServiceDesk.Infrastructure.Ai;

// The "Ai:OpenAICompatible" configuration section. AGENTS.md §8, §9: the ApiKey comes only from User Secrets or an
// environment variable (Ai__OpenAICompatible__ApiKey), never from appsettings*.json.
internal sealed class OpenAICompatibleOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? ApiKey { get; set; }
}
