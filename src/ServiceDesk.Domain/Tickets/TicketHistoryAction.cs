namespace ServiceDesk.Domain.Tickets;

public enum TicketHistoryAction
{
    Created,
    Assigned,
    Reassigned,
    Unassigned,
    StatusChanged,
    Recategorized,
    ImpactChanged,
    UrgencyChanged,
}
