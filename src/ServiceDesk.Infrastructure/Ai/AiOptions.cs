namespace ServiceDesk.Infrastructure.Ai;

// The "Ai" configuration section (AGENTS.md §9), checked by AiOptionsValidator when the host starts.
internal sealed class AiOptions
{
    public const string SectionName = "Ai";

    public AiProvider Provider { get; set; }

    public int TimeoutSeconds { get; set; }

    public string PromptVersion { get; set; } = string.Empty;

    public OllamaOptions Ollama { get; set; } = new();

    public OpenAICompatibleOptions OpenAICompatible { get; set; } = new();

    // The model of the selected provider, saved with every suggestion (BR-09).
    public string GetModel() => Provider == AiProvider.OpenAICompatible ? OpenAICompatible.Model : Ollama.Model;
}
