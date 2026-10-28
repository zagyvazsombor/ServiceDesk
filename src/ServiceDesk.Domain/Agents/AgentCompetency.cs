namespace ServiceDesk.Domain.Agents;

public sealed class AgentCompetency
{
    // Used by EF Core and the factory method.
    private AgentCompetency()
    {
    }

    public Guid AgentId { get; private set; }

    public Guid CategoryId { get; private set; }

    internal static AgentCompetency Create(Guid agentId, Guid categoryId) =>
        new() { AgentId = agentId, CategoryId = categoryId };
}
