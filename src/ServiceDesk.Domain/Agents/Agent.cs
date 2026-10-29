using ServiceDesk.Domain.Categories;
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

    // BR-04: only an active agent with a competency for the category may hold a ticket of that category.
    public bool IsEligibleFor(Guid categoryId) => IsActive && HasCompetency(categoryId);

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

    // The Admin use case: replaces the competency set like SetCompetencies, with the categories themselves. A new
    // competency needs an active category; one the agent already has stays, even if its category was deactivated.
    public Result UpdateCompetencies(IReadOnlyCollection<Category> categories)
    {
        ArgumentNullException.ThrowIfNull(categories);

        if (categories.Any(category => !category.IsActive && !HasCompetency(category.Id)))
        {
            return CategoryErrors.Inactive; // BR-13
        }

        return SetCompetencies(categories.Select(category => category.Id));
    }

    public void Activate() => IsActive = true;

    // BR-12: deactivation does not touch the tickets already assigned to the agent.
    public void Deactivate() => IsActive = false;

    // BR-05: LastAssignedAt is the tie-breaker of the automatic assignment.
    public void RecordAssignment(DateTimeOffset now) => LastAssignedAt = now;
}
