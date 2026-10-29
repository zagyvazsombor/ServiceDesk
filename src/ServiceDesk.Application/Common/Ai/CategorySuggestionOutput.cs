namespace ServiceDesk.Application.Common.Ai;

// AGENTS.md §9: the validated model answer (BR-08). CategoryCode is the code of one of the input categories,
// Confidence is between 0 and 1, Reasoning is plain text of at most CategorySuggestion.ReasoningMaxLength characters.
public sealed record CategorySuggestionOutput(
    string CategoryCode,
    double Confidence,
    string Reasoning,
    string Model,
    string PromptVersion);
