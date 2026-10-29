namespace ServiceDesk.Infrastructure.Ai;

// AGENTS.md §9: the structured output requested from the model (JSON schema, camelCase property names). It is
// untrusted: LlmCategorySuggester validates every field before use (BR-08).
internal sealed record LlmCategoryAnswer(string CategoryCode, double Confidence, string Reasoning);
