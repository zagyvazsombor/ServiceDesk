using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Assignment;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Application.Tests;

// Valid domain objects for the handler tests; the repositories that would load them are substituted.
internal static class TestData
{
    public const string ValidTitle = "Nem indul a laptop";
    public const string ValidDescription = "Bekapcsoláskor a laptop képernyője fekete marad.";

    public static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.FromHours(1));

    public static Category CreateCategory(string code = "HARDWARE") =>
        Category.Create(code, "Hardver", "Számítógépek, perifériák és egyéb eszközök hibái.").Value;

    public static Category CreateInactiveCategory()
    {
        var category = CreateCategory("OTHER");
        category.Deactivate();
        return category;
    }

    public static Agent CreateAgent(string displayName, params Category[] competencies)
    {
        var agent = Agent.Create($"user-{Guid.NewGuid()}", displayName).Value;
        EnsureSuccess(agent.SetCompetencies(competencies.Select(category => category.Id)));
        return agent;
    }

    // The candidate the IAgentRepository would return for the agent (BR-04, BR-05).
    public static AgentCandidate CandidateOf(Agent agent, params Priority[] openTicketPriorities) =>
        new(agent.Id, agent.LastAssignedAt, openTicketPriorities);

    public static Ticket CreateTicket(Category category) =>
        Ticket.Create(TestUsers.UserId, ValidTitle, ValidDescription, Impact.Medium, Urgency.Medium, category, Now).Value;

    private static void EnsureSuccess(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test arrangement failed: {result.Error.Code}");
        }
    }
}
