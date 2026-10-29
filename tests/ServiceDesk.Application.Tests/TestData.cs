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

    // The Admin of the arranged status changes; differs from TestUsers.UserId, so a test can tell the two apart.
    public const string ArrangingAdminUserId = "admin-arrange";

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

    // Moves a new ticket along the main path (assign, start, resolve, close) until it reaches the given status.
    public static Ticket CreateTicketInStatus(TicketStatus status, Category category, Agent assignee)
    {
        var ticket = CreateTicket(category);
        Func<Result>[] steps =
        [
            () => ticket.AssignTo(assignee, null, Now),
            () => ticket.Start(ArrangingAdminUserId, Now),
            () => ticket.Resolve(ArrangingAdminUserId, Now),
            () => ticket.Close(ArrangingAdminUserId, Now),
        ];
        var stepCount = status switch
        {
            TicketStatus.New => 0,
            TicketStatus.Assigned => 1,
            TicketStatus.InProgress => 2,
            TicketStatus.Resolved => 3,
            TicketStatus.Closed => 4,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown status."),
        };

        foreach (var step in steps.Take(stepCount))
        {
            EnsureSuccess(step());
        }

        return ticket;
    }

    private static void EnsureSuccess(Result result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException($"Test arrangement failed: {result.Error.Code}");
        }
    }
}
