using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;

namespace ServiceDesk.Domain.Tickets;

public sealed class Ticket
{
    public const int TitleMinLength = 5;
    public const int TitleMaxLength = 200;
    public const int DescriptionMinLength = 10;
    public const int DescriptionMaxLength = 4000;

    private readonly List<TicketHistoryEntry> _history = [];

    // Used by EF Core and the factory method.
    private Ticket()
    {
    }

    public Guid Id { get; private set; }

    // BR-01: set once by the factory, never changed afterwards.
    public string ReporterId { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public Impact Impact { get; private set; }

    public Urgency Urgency { get; private set; }

    // BR-11: stored for querying, but only ever set from the Impact × Urgency matrix.
    public Priority Priority { get; private set; }

    public Guid CategoryId { get; private set; }

    public TicketStatus Status { get; private set; }

    public Guid? AssigneeAgentId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyCollection<TicketHistoryEntry> History => _history.AsReadOnly();

    public static Result<Ticket> Create(
        string reporterId,
        string title,
        string description,
        Impact impact,
        Urgency urgency,
        Category category,
        DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reporterId);
        ArgumentNullException.ThrowIfNull(title);
        ArgumentNullException.ThrowIfNull(description);
        ArgumentNullException.ThrowIfNull(category);

        var trimmedTitle = title.Trim();
        if (trimmedTitle.Length is < TitleMinLength or > TitleMaxLength)
        {
            return TicketErrors.TitleLength; // BR-01
        }

        var trimmedDescription = description.Trim();
        if (trimmedDescription.Length is < DescriptionMinLength or > DescriptionMaxLength)
        {
            return TicketErrors.DescriptionLength; // BR-01
        }

        if (!Enum.IsDefined(impact))
        {
            return TicketErrors.ImpactInvalid; // BR-01
        }

        if (!Enum.IsDefined(urgency))
        {
            return TicketErrors.UrgencyInvalid; // BR-01
        }

        if (!category.IsActive)
        {
            return CategoryErrors.Inactive; // BR-13
        }

        var ticket = new Ticket
        {
            Id = Guid.CreateVersion7(),
            ReporterId = reporterId,
            Title = trimmedTitle,
            Description = trimmedDescription,
            Impact = impact,
            Urgency = urgency,
            Priority = PriorityMatrix.Calculate(impact, urgency), // BR-11
            CategoryId = category.Id,
            Status = TicketStatus.New,
            CreatedAt = now,
        };
        ticket.AddHistory(reporterId, now, TicketHistoryAction.Created, null, TicketStatus.New.ToString()); // BR-10

        return ticket;
    }

    // BR-11: only an Admin changes impact and urgency (triage); the priority is recalculated.
    public Result ChangeImpactAndUrgency(Impact impact, Urgency urgency, string actorUserId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUserId);

        if (Status == TicketStatus.Closed)
        {
            return TicketErrors.ClosedCannotBeModified; // BR-03
        }

        if (!Enum.IsDefined(impact))
        {
            return TicketErrors.ImpactInvalid;
        }

        if (!Enum.IsDefined(urgency))
        {
            return TicketErrors.UrgencyInvalid;
        }

        if (impact != Impact)
        {
            AddHistory(actorUserId, now, TicketHistoryAction.ImpactChanged, Impact.ToString(), impact.ToString()); // BR-10
            Impact = impact;
        }

        if (urgency != Urgency)
        {
            AddHistory(actorUserId, now, TicketHistoryAction.UrgencyChanged, Urgency.ToString(), urgency.ToString()); // BR-10
            Urgency = urgency;
        }

        Priority = PriorityMatrix.Calculate(Impact, Urgency); // BR-11
        return Result.Success();
    }

    // Changes the category only; keeping, reassigning or unassigning the assignee (BR-07) is up to the caller.
    public Result Recategorize(Category category, string actorUserId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(category);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUserId);

        if (Status == TicketStatus.Closed)
        {
            return TicketErrors.ClosedCannotBeModified; // BR-03
        }

        if (Status is not (TicketStatus.New or TicketStatus.Assigned or TicketStatus.InProgress))
        {
            return TicketErrors.RecategorizationNotAllowed; // BR-07
        }

        if (!category.IsActive)
        {
            return CategoryErrors.Inactive; // BR-13
        }

        if (category.Id == CategoryId)
        {
            return Result.Success();
        }

        AddHistory(actorUserId, now, TicketHistoryAction.Recategorized, CategoryId.ToString(), category.Id.ToString()); // BR-10
        CategoryId = category.Id;
        return Result.Success();
    }

    // actorUserId: null = system (automatic assignment, BR-05), otherwise the Admin who assigns manually.
    public Result AssignTo(Agent agent, string? actorUserId, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(agent);

        if (Status == TicketStatus.Closed)
        {
            return TicketErrors.ClosedCannotBeAssigned; // BR-03
        }

        if (!agent.IsActive)
        {
            return AgentErrors.Inactive; // BR-04, BR-12
        }

        if (!agent.HasCompetency(CategoryId))
        {
            return AgentErrors.MissingCompetency; // BR-04
        }

        if (AssigneeAgentId == agent.Id)
        {
            return Result.Success();
        }

        if (Status == TicketStatus.New)
        {
            AddHistory(actorUserId, now, TicketHistoryAction.Assigned, null, agent.Id.ToString()); // BR-10
            AssigneeAgentId = agent.Id;
            ChangeStatus(TicketStatus.Assigned, actorUserId, now);
        }
        else
        {
            // Reassignment keeps the status.
            AddHistory(actorUserId, now, TicketHistoryAction.Reassigned, AssigneeAgentId?.ToString(), agent.Id.ToString()); // BR-10
            AssigneeAgentId = agent.Id;
        }

        agent.RecordAssignment(now); // BR-05
        return Result.Success();
    }

    // Done by the system, only when no agent is eligible after a recategorization (BR-07).
    public Result Unassign(DateTimeOffset now)
    {
        if (Status is not (TicketStatus.Assigned or TicketStatus.InProgress))
        {
            return TicketErrors.InvalidTransition;
        }

        AddHistory(null, now, TicketHistoryAction.Unassigned, AssigneeAgentId?.ToString(), null); // BR-10
        AssigneeAgentId = null;
        ChangeStatus(TicketStatus.New, null, now); // BR-07
        return Result.Success();
    }

    public Result Start(string actorUserId, DateTimeOffset now) =>
        Transition(TicketStatus.Assigned, TicketStatus.InProgress, actorUserId, now);

    public Result Resolve(string actorUserId, DateTimeOffset now) =>
        Transition(TicketStatus.InProgress, TicketStatus.Resolved, actorUserId, now);

    public Result Reopen(string actorUserId, DateTimeOffset now) =>
        Transition(TicketStatus.Resolved, TicketStatus.InProgress, actorUserId, now);

    public Result Close(string actorUserId, DateTimeOffset now) =>
        Transition(TicketStatus.Resolved, TicketStatus.Closed, actorUserId, now);

    private Result Transition(TicketStatus from, TicketStatus to, string actorUserId, DateTimeOffset now)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(actorUserId);

        // BR-03: no transition starts from Closed, so a closed ticket is terminal.
        if (Status != from)
        {
            return TicketErrors.InvalidTransition;
        }

        ChangeStatus(to, actorUserId, now);
        return Result.Success();
    }

    private void ChangeStatus(TicketStatus newStatus, string? actorUserId, DateTimeOffset now)
    {
        AddHistory(actorUserId, now, TicketHistoryAction.StatusChanged, Status.ToString(), newStatus.ToString()); // BR-10
        Status = newStatus;
    }

    // Entries are never removed and the whole history is always loaded, so the next sequence is count + 1.
    private void AddHistory(
        string? actorUserId,
        DateTimeOffset now,
        TicketHistoryAction action,
        string? oldValue,
        string? newValue) =>
        _history.Add(TicketHistoryEntry.Create(_history.Count + 1, actorUserId, now, action, oldValue, newValue));
}
