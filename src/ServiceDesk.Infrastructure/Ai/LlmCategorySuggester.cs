using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServiceDesk.Application.Common.Ai;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Domain.CategorySuggestions;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Infrastructure.Ai;

// AGENTS.md §9: the ICategorySuggester adapter over IChatClient, with structured output and without tools.
// Every failure of the model is AiErrors.NoSuggestion with a Warning log (BR-08); the logs never contain the ticket
// text or the answer of the model, which can repeat it.
internal sealed partial class LlmCategorySuggester(
    IChatClient chatClient,
    IOptions<AiOptions> options,
    TimeProvider timeProvider,
    ILogger<LlmCategorySuggester> logger) : ICategorySuggester
{
    public async Task<Result<CategorySuggestionOutput>> SuggestAsync(CategorySuggestionInput input, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(input);

        var aiOptions = options.Value;
        var model = aiOptions.GetModel();
        var messages = LlmCategoryPrompt.BuildMessages(input);
        // A classification, so the most likely answer is wanted. No Tools: AGENTS.md §9 forbids tool calling.
        var chatOptions = new ChatOptions { Temperature = 0 };

        // The timeout runs on the TimeProvider, so a test can move past it.
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(aiOptions.TimeoutSeconds), timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token);

        ChatResponse<LlmCategoryAnswer> response;
        try
        {
            response = await chatClient.GetResponseAsync<LlmCategoryAnswer>(
                messages,
                chatOptions,
                useJsonSchemaResponseFormat: true,
                linked.Token);
        }
        catch (OperationCanceledException) when (timeout.IsCancellationRequested && !ct.IsCancellationRequested)
        {
            LogTimedOut(logger, model, aiOptions.TimeoutSeconds);
            return AiErrors.NoSuggestion;
        }
        // E.g. Ollama is not running (HttpRequestException) or the model is missing. A cancellation of the caller is
        // not a model failure, so it is thrown on.
        catch (Exception exception) when (!ct.IsCancellationRequested)
        {
            LogFailed(logger, exception, model);
            return AiErrors.NoSuggestion;
        }

        return Validate(response, input.Categories, model);
    }

    // BR-08: a code of the input (active) categories, a confidence clamped to 0–1, plain text reasoning.
    private Result<CategorySuggestionOutput> Validate(
        ChatResponse<LlmCategoryAnswer> response,
        IReadOnlyList<SuggestionCategory> categories,
        string model)
    {
        if (!response.TryGetResult(out var answer) || answer is null)
        {
            LogInvalidAnswer(logger, model, "the answer is not a JSON object of the requested shape");
            return AiErrors.NoSuggestion;
        }

        var code = answer.CategoryCode?.Trim();
        var category = categories.FirstOrDefault(candidate => string.Equals(candidate.Code, code, StringComparison.OrdinalIgnoreCase));
        if (category is null)
        {
            LogInvalidAnswer(logger, model, "the category code is not one of the active categories");
            return AiErrors.NoSuggestion;
        }

        if (double.IsNaN(answer.Confidence))
        {
            LogInvalidAnswer(logger, model, "the confidence is not a number");
            return AiErrors.NoSuggestion;
        }

        return new CategorySuggestionOutput(
            category.Code,
            Math.Clamp(answer.Confidence, 0, 1),
            ToPlainText(answer.Reasoning),
            model,
            LlmCategoryPrompt.Version);
    }

    // Control characters (line breaks too) become spaces; at most CategorySuggestion.ReasoningMaxLength characters,
    // without splitting a surrogate pair. The UI shows it as text, never as markup (AGENTS.md §6).
    private static string ToPlainText(string? reasoning)
    {
        if (string.IsNullOrWhiteSpace(reasoning))
        {
            return string.Empty;
        }

        var text = new string(reasoning.Select(character => char.IsControl(character) ? ' ' : character).ToArray()).Trim();
        if (text.Length <= CategorySuggestion.ReasoningMaxLength)
        {
            return text;
        }

        var length = char.IsHighSurrogate(text[CategorySuggestion.ReasoningMaxLength - 1])
            ? CategorySuggestion.ReasoningMaxLength - 1
            : CategorySuggestion.ReasoningMaxLength;
        return text[..length].TrimEnd();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Category suggestion of model {Model} timed out after {TimeoutSeconds} seconds")]
    private static partial void LogTimedOut(ILogger logger, string model, int timeoutSeconds);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Category suggestion of model {Model} failed")]
    private static partial void LogFailed(ILogger logger, Exception exception, string model);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Category suggestion of model {Model} rejected: {Reason}")]
    private static partial void LogInvalidAnswer(ILogger logger, string model, string reason);
}
