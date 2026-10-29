namespace ServiceDesk.Application.Tickets.GetTicketQueueFilters;

// Id: the value of GetTicketQueueQuery.CategoryId or AssigneeAgentId. IsActive is false for an inactive category or
// agent, which still stays on the existing tickets (BR-12, BR-13).
public sealed record QueueFilterOptionDto(Guid Id, string Name, bool IsActive);
