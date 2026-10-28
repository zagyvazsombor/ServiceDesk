using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;

namespace ServiceDesk.Domain.Tests;

internal static class TestData
{
    public const string ReporterId = "reporter-dora";
    public const string AdminUserId = "admin-anna";
    public const string ValidTitle = "Nem indul a laptop";
    public const string ValidDescription = "Bekapcsoláskor a laptop képernyője fekete marad.";

    public static readonly DateTimeOffset Now = new(2026, 10, 28, 9, 0, 0, TimeSpan.FromHours(1));
    public static readonly DateTimeOffset Later = Now.AddHours(2);

    public static Category CreateCategory(string code = "HARDWARE") =>
        Category.Create(code, "Hardver", "Számítógépek, perifériák és egyéb eszközök hibái.").Value;

    public static Category CreateInactiveCategory(string code = "OTHER")
    {
        var category = CreateCategory(code);
        category.Deactivate();
        return category;
    }

    public static Agent CreateAgent(params Category[] competencies)
    {
        var agent = Agent.Create($"user-{Guid.NewGuid()}", "Kiss Anna").Value;
        EnsureSuccess(agent.SetCompetencies(competencies.Select(category => category.Id)));
        return agent;
    }

    public static Ticket CreateTicket(
        Category category,
        Impact impact = Impact.Medium,
        Urgency urgency = Urgency.Medium) =>
        Ticket.Create(ReporterId, ValidTitle, ValidDescription, impact, urgency, category, Now).Value;

    // Moves a new ticket along the main path (assign, start, resolve, close) until it reaches the given status.
    public static Ticket CreateTicketInStatus(TicketStatus status, Category category, Agent assignee)
    {
        var ticket = CreateTicket(category);
        Func<Result>[] steps =
        [
            () => ticket.AssignTo(assignee, null, Now),
            () => ticket.Start(AdminUserId, Now),
            () => ticket.Resolve(AdminUserId, Now),
            () => ticket.Close(AdminUserId, Now),
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
