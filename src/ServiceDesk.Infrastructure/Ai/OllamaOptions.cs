namespace ServiceDesk.Infrastructure.Ai;

// The "Ai:Ollama" configuration section.
internal sealed class OllamaOptions
{
    public string Endpoint { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;
}
