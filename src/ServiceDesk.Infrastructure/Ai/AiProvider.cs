namespace ServiceDesk.Infrastructure.Ai;

// AGENTS.md §9: the default is a local Ollama; the alternative is an OpenAI-compatible endpoint.
internal enum AiProvider
{
    Ollama,
    OpenAICompatible,
}
