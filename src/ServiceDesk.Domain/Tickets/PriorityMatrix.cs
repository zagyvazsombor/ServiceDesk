namespace ServiceDesk.Domain.Tickets;

public static class PriorityMatrix
{
    // BR-11: Impact × Urgency matrix.
    public static Priority Calculate(Impact impact, Urgency urgency)
    {
        if (!Enum.IsDefined(impact))
        {
            throw new ArgumentOutOfRangeException(nameof(impact), impact, "Unknown impact.");
        }

        return (impact, urgency) switch
        {
            (Impact.High, Urgency.High) => Priority.Critical,
            (Impact.High, Urgency.Medium) => Priority.High,
            (Impact.High, Urgency.Low) => Priority.Medium,
            (Impact.Medium, Urgency.High) => Priority.High,
            (Impact.Medium, Urgency.Medium) => Priority.Medium,
            (Impact.Medium, Urgency.Low) => Priority.Low,
            (Impact.Low, Urgency.High) => Priority.Medium,
            (Impact.Low, Urgency.Medium) => Priority.Low,
            (Impact.Low, Urgency.Low) => Priority.Planning,
            _ => throw new ArgumentOutOfRangeException(nameof(urgency), urgency, "Unknown urgency."),
        };
    }

    // Load weight of an open ticket, used by the automatic assignment (BR-05).
    public static int GetLoadWeight(Priority priority) => priority switch
    {
        Priority.Critical => 5,
        Priority.High => 4,
        Priority.Medium => 3,
        Priority.Low => 2,
        Priority.Planning => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(priority), priority, "Unknown priority."),
    };
}
