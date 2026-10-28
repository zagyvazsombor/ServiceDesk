using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Infrastructure.Identity;

namespace ServiceDesk.Infrastructure.Tests.Builders;

internal sealed class TicketBuilder(ApplicationUser reporter, Category category)
{
    private Impact _impact = Impact.Medium;
    private Urgency _urgency = Urgency.Medium;
    private (TicketStatus Status, Agent Assignee, ApplicationUser Admin)? _target;

    public TicketBuilder WithImpactAndUrgency(Impact impact, Urgency urgency)
    {
        _impact = impact;
        _urgency = urgency;
        return this;
    }

    // The system assigns the ticket to the agent, then the admin moves it along the main path (start, resolve, close)
    // until it reaches the status; each step happens one minute after the previous one.
    public TicketBuilder InStatus(TicketStatus status, Agent assignee, ApplicationUser admin)
    {
        _target = (status, assignee, admin);
        return this;
    }

    public Ticket Build()
    {
        var ticket = Ticket.Create(
            reporter.Id,
            "Nem indul a laptop",
            "Bekapcsoláskor a laptop képernyője fekete marad.",
            _impact,
            _urgency,
            category,
            TestTime.Now).Value;
        if (_target is not (var status, var assignee, var admin))
        {
            return ticket;
        }

        Func<DateTimeOffset, Result>[] steps =
        [
            now => ticket.AssignTo(assignee, null, now),
            now => ticket.Start(admin.Id, now),
            now => ticket.Resolve(admin.Id, now),
            now => ticket.Close(admin.Id, now),
        ];
        var stepCount = status switch
        {
            TicketStatus.New => 0,
            TicketStatus.Assigned => 1,
            TicketStatus.InProgress => 2,
            TicketStatus.Resolved => 3,
            TicketStatus.Closed => 4,
            _ => throw new InvalidOperationException($"Unknown status: {status}."),
        };

        for (var step = 0; step < stepCount; step++)
        {
            steps[step](TestTime.Now.AddMinutes(step + 1)).EnsureSuccess();
        }

        return ticket;
    }
}
