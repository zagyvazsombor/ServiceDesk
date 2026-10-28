using ServiceDesk.Domain.Common;

namespace ServiceDesk.Domain.Agents;

public sealed class Agent
{
    public const int DisplayNameMinLength = 2;
    public const int DisplayNameMaxLength = 100;

    private readonly List<AgentCompetency> _competencies = [];

    // Used by EF Core and the factory method.
    private Agent()
    {
    }

    public Guid Id { get; private set; }

    public string UserId { get; private set; } = string.Empty;

    public string DisplayName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset? LastAssignedAt { get; private set; }

    public IReadOnlyCollection<AgentCompetency> Competencies => _competencies.AsReadOnly();

    public static Result<Agent> Create(string userId, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentNullException.ThrowIfNull(displayName);

        var trimmedDisplayName = displayName.Trim();
        if (trimmedDisplayName.Length is < DisplayNameMinLength or > DisplayNameMaxLength)
        {
            return AgentErrors.DisplayNameLength;
        }

        return new Agent
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            DisplayName = trimmedDisplayName,
            IsActive = true,
        };
    }

    public bool HasCompetency(Guid categoryId) =>
        _competencies.Exists(competency => competency.CategoryId == categoryId);

    // Replaces the whole competency set; entries that stay are kept, so only the difference changes.
    public Result SetCompetencies(IEnumerable<Guid> categoryIds)
    {
        ArgumentNullException.ThrowIfNull(categoryIds);

        var newCategoryIds = categoryIds.ToHashSet();
        if (newCategoryIds.Contains(Guid.Empty))
        {
            return AgentErrors.CompetencyCategoryInvalid;
        }

        _competencies.RemoveAll(competency => !newCategoryIds.Contains(competency.CategoryId));
        foreach (var categoryId in newCategoryIds)
        {
            if (!HasCompetency(categoryId))
            {
                _competencies.Add(AgentCompetency.Create(Id, categoryId));
            }
        }

        return Result.Success();
    }

    public void Activate() => IsActive = true;

    // BR-12: deactivation does not touch the tickets already assigned to the agent.
    public void Deactivate() => IsActive = false;

    // BR-05: LastAssignedAt is the tie-breaker of the automatic assignment.
    public void RecordAssignment(DateTimeOffset now) => LastAssignedAt = now;
}
