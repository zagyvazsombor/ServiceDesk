using Microsoft.Extensions.Options;
using ServiceDesk.Domain.CategorySuggestions;

namespace ServiceDesk.Infrastructure.Ai;

// AGENTS.md §9: registered with ValidateOnStart, so a wrong Ai section stops the host at start-up instead of failing
// every suggestion. Only the selected provider has to be configured.
internal sealed class AiOptionsValidator : IValidateOptions<AiOptions>
{
    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();
        if (options.TimeoutSeconds <= 0)
        {
            failures.Add("Ai:TimeoutSeconds must be a positive number of seconds.");
        }

        if (options.PromptVersion != LlmCategoryPrompt.Version)
        {
            failures.Add($"Ai:PromptVersion must be '{LlmCategoryPrompt.Version}', the only prompt version in the code.");
        }

        switch (options.Provider)
        {
            case AiProvider.Ollama:
                ValidateEndpointAndModel("Ai:Ollama", options.Ollama.Endpoint, options.Ollama.Model, failures);
                break;
            case AiProvider.OpenAICompatible:
                ValidateEndpointAndModel("Ai:OpenAICompatible", options.OpenAICompatible.Endpoint, options.OpenAICompatible.Model, failures);
                if (string.IsNullOrWhiteSpace(options.OpenAICompatible.ApiKey))
                {
                    failures.Add("Ai:OpenAICompatible:ApiKey is required; set it in User Secrets or in an environment variable.");
                }

                break;
            default:
                failures.Add($"Ai:Provider must be one of: {string.Join(", ", Enum.GetNames<AiProvider>())}.");
                break;
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateEndpointAndModel(string section, string endpoint, string model, List<string> failures)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            failures.Add($"{section}:Endpoint must be an absolute http or https URL.");
        }

        // The model name is saved with every suggestion (BR-09).
        if (string.IsNullOrWhiteSpace(model) || model.Length > CategorySuggestion.ModelMaxLength)
        {
            failures.Add($"{section}:Model is required and can be at most {CategorySuggestion.ModelMaxLength} characters long.");
        }
    }
}
