using System.ClientModel;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OllamaSharp;
using OpenAI;

namespace ServiceDesk.Infrastructure.Ai;

internal static class ChatClientFactory
{
    // AGENTS.md §9: the provider of the validated Ai options; called once, when the chat client is first resolved.
    public static IChatClient Create(IServiceProvider services)
    {
        var options = services.GetRequiredService<IOptions<AiOptions>>().Value;

        return options.Provider switch
        {
            AiProvider.Ollama => new OllamaApiClient(new Uri(options.Ollama.Endpoint), options.Ollama.Model),
            AiProvider.OpenAICompatible => new OpenAIClient(
                    new ApiKeyCredential(options.OpenAICompatible.ApiKey!),
                    new OpenAIClientOptions { Endpoint = new Uri(options.OpenAICompatible.Endpoint) })
                .GetChatClient(options.OpenAICompatible.Model)
                .AsIChatClient(),
            _ => throw new InvalidOperationException($"Unknown AI provider: {options.Provider}."),
        };
    }
}
