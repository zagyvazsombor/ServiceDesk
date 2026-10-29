namespace ServiceDesk.Domain.Tickets;

// AGENTS.md §10: the operations the state machine and the business rules allow in a status. Ticket (and the automatic
// assignment rerun) enforce them through these methods; the UI only reads them to hide what would be refused anyway.
public static class TicketWorkflow
{
    // BR-03: no operation changes a closed ticket.
    public static bool IsTerminal(TicketStatus status) => status == TicketStatus.Closed;

    // Assigned → InProgress.
    public static bool CanStart(TicketStatus status) => status == TicketStatus.Assigned;

    // InProgress → Resolved.
    public static bool CanResolve(TicketStatus status) => status == TicketStatus.InProgress;

    // Resolved → InProgress.
    public static bool CanReopen(TicketStatus status) => status == TicketStatus.Resolved;

    // Resolved → Closed.
    public static bool CanClose(TicketStatus status) => status == TicketStatus.Resolved;

    // Assigned or InProgress → New; done by the system when nobody is eligible after a recategorization (BR-07).
    public static bool CanUnassign(TicketStatus status) =>
        status is TicketStatus.Assigned or TicketStatus.InProgress;

    // Assignment and reassignment (BR-03).
    public static bool CanAssign(TicketStatus status) => !IsTerminal(status);

    // Triage (BR-03, BR-11).
    public static bool CanChangeImpactAndUrgency(TicketStatus status) => !IsTerminal(status);

    // BR-07.
    public static bool CanRecategorize(TicketStatus status) =>
        status is TicketStatus.New or TicketStatus.Assigned or TicketStatus.InProgress;

    // BR-06: only a ticket left without an assignee; a New ticket never has one.
    public static bool CanRetryAutoAssign(TicketStatus status) => status == TicketStatus.New;
}
