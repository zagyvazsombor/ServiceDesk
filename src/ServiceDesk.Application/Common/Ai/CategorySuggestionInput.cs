namespace ServiceDesk.Application.Common.Ai;

// AGENTS.md §9: everything the model gets. There is no user Id or e-mail address in it.
// Categories: the active categories (BR-08, BR-13); the suggestion is one of them.
public sealed record CategorySuggestionInput(
    string Title,
    string Description,
    IReadOnlyList<SuggestionCategory> Categories);
