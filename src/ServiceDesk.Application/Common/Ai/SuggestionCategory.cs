namespace ServiceDesk.Application.Common.Ai;

// A category the model can choose from; the LLM prompt lists it with its Hungarian name and description.
public sealed record SuggestionCategory(string Code, string Name, string Description);
