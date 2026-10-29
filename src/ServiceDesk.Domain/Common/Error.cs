using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;

namespace ServiceDesk.Domain.Common;

// Code format: "Ticket.ClosedCannotBeAssigned"; validation errors use "Validation.<Property>". Message is Hungarian.
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "The type name is fixed by AGENTS.md §11; the solution is C#-only, so the Visual Basic keyword does not apply.")]
public sealed record Error(string Code, string Message, ErrorType Type)
{
    private const string ValidationCodePrefix = "Validation.";

    // AGENTS.md §11: every invalid field (property name → its messages). Only Validation() fills it; empty otherwise.
    public IReadOnlyDictionary<string, IReadOnlyList<string>> FieldErrors { get; private init; } =
        ReadOnlyDictionary<string, IReadOnlyList<string>>.Empty;

    // Code and Message are those of the first failure, so callers that read a single error keep working.
    public static Error Validation(IReadOnlyList<(string Field, string Message)> failures)
    {
        ArgumentNullException.ThrowIfNull(failures);
        if (failures.Count == 0)
        {
            throw new ArgumentException("A validation error needs at least one field error.", nameof(failures));
        }

        var fieldErrors = failures
            .GroupBy(failure => failure.Field, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(failure => failure.Message).ToList().AsReadOnly(),
                StringComparer.Ordinal)
            .AsReadOnly();

        var (field, message) = failures[0];
        return new Error(ValidationCodePrefix + field, message, ErrorType.Validation) { FieldErrors = fieldErrors };
    }

    // The synthesized equality would compare the FieldErrors dictionaries by reference.
    public bool Equals(Error? other) =>
        other is not null
        && Code == other.Code
        && Message == other.Message
        && Type == other.Type
        && FieldErrors.Count == other.FieldErrors.Count
        && FieldErrors.All(field => other.FieldErrors.TryGetValue(field.Key, out var messages)
            && messages.SequenceEqual(field.Value));

    public override int GetHashCode() => HashCode.Combine(Code, Message, Type, FieldErrors.Count);
}
