namespace ServiceDesk.Application.Tickets.GetTicketQueueFilters;

// Every category and every agent by name, the inactive ones included.
public sealed record TicketQueueFiltersDto(
    IReadOnlyList<QueueFilterOptionDto> Categories,
    IReadOnlyList<QueueFilterOptionDto> Agents);
