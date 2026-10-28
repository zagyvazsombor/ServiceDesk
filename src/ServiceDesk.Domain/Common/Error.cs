using System.Diagnostics.CodeAnalysis;

namespace ServiceDesk.Domain.Common;

// Code format: "Ticket.ClosedCannotBeAssigned"; validation errors use "Validation.<Property>". Message is Hungarian.
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "The type name is fixed by AGENTS.md §11; the solution is C#-only, so the Visual Basic keyword does not apply.")]
public sealed record Error(string Code, string Message, ErrorType Type);
