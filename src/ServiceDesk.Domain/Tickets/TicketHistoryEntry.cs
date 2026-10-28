namespace ServiceDesk.Domain.Tickets;

// BR-10: audit entry inside the Ticket aggregate.
public sealed class TicketHistoryEntry
{
    public const int ValueMaxLength = 200;

    // Used by EF Core and the factory method.
    private TicketHistoryEntry()
    {
    }

    public Guid Id { get; private set; }

    // Position of the entry within its ticket: 1, 2, 3, … in the order of the changes.
    public int Sequence { get; private set; }

    // null = system.
    public string? ActorUserId { get; private set; }

    public DateTimeOffset At { get; private set; }

    public TicketHistoryAction Action { get; private set; }

    public string? OldValue { get; private set; }

    public string? NewValue { get; private set; }

    internal static TicketHistoryEntry Create(
        int sequence,
        string? actorUserId,
        DateTimeOffset at,
        TicketHistoryAction action,
        string? oldValue,
        string? newValue)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sequence, 1);
        ThrowIfTooLong(oldValue, nameof(oldValue));
        ThrowIfTooLong(newValue, nameof(newValue));

        return new TicketHistoryEntry
        {
            Id = Guid.CreateVersion7(),
            Sequence = sequence,
            ActorUserId = actorUserId,
            At = at,
            Action = action,
            OldValue = oldValue,
            NewValue = newValue,
        };
    }

    private static void ThrowIfTooLong(string? value, string parameterName)
    {
        if (value?.Length > ValueMaxLength)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                $"A history value can be at most {ValueMaxLength} characters long.");
        }
    }
}
