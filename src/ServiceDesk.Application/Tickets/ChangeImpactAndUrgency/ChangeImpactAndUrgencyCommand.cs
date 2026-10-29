using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tickets.ChangeImpactAndUrgency;

// BR-11: triage sets the impact and the urgency, never the priority. Impact and Urgency are nullable for the same
// reason as in CreateTicketCommand: their first value (High) would otherwise stand in for a missing choice.
public sealed record ChangeImpactAndUrgencyCommand(Guid TicketId, Impact? Impact, Urgency? Urgency);
