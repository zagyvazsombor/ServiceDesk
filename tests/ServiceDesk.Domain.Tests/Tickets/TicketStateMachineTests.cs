using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Domain.Tests.Tickets;

// Every (status, operation) pair of the Ticket aggregate (AGENTS.md §10, state machine).
public sealed class TicketStateMachineTests
{
    private const string InvalidTransition = "Ticket.InvalidTransition";
    private const string ClosedCannotBeAssigned = "Ticket.ClosedCannotBeAssigned";
    private const string ClosedCannotBeModified = "Ticket.ClosedCannotBeModified";
    private const string RecategorizationNotAllowed = "Ticket.RecategorizationNotAllowed";

    public enum Operation
    {
        AssignTo,
        Start,
        Resolve,
        Reopen,
        Close,
        Unassign,
        Recategorize,
        ChangeImpactAndUrgency,
    }

    [Theory]
    [Trait("BR", "BR-03")]
    [Trait("BR", "BR-07")]
    [InlineData(TicketStatus.New, Operation.AssignTo, TicketStatus.Assigned, null)]
    [InlineData(TicketStatus.New, Operation.Start, TicketStatus.New, InvalidTransition)]
    [InlineData(TicketStatus.New, Operation.Resolve, TicketStatus.New, InvalidTransition)]
    [InlineData(TicketStatus.New, Operation.Reopen, TicketStatus.New, InvalidTransition)]
    [InlineData(TicketStatus.New, Operation.Close, TicketStatus.New, InvalidTransition)]
    [InlineData(TicketStatus.New, Operation.Unassign, TicketStatus.New, InvalidTransition)]
    [InlineData(TicketStatus.New, Operation.Recategorize, TicketStatus.New, null)]
    [InlineData(TicketStatus.New, Operation.ChangeImpactAndUrgency, TicketStatus.New, null)]
    [InlineData(TicketStatus.Assigned, Operation.AssignTo, TicketStatus.Assigned, null)]
    [InlineData(TicketStatus.Assigned, Operation.Start, TicketStatus.InProgress, null)]
    [InlineData(TicketStatus.Assigned, Operation.Resolve, TicketStatus.Assigned, InvalidTransition)]
    [InlineData(TicketStatus.Assigned, Operation.Reopen, TicketStatus.Assigned, InvalidTransition)]
    [InlineData(TicketStatus.Assigned, Operation.Close, TicketStatus.Assigned, InvalidTransition)]
    [InlineData(TicketStatus.Assigned, Operation.Unassign, TicketStatus.New, null)]
    [InlineData(TicketStatus.Assigned, Operation.Recategorize, TicketStatus.Assigned, null)]
    [InlineData(TicketStatus.Assigned, Operation.ChangeImpactAndUrgency, TicketStatus.Assigned, null)]
    [InlineData(TicketStatus.InProgress, Operation.AssignTo, TicketStatus.InProgress, null)]
    [InlineData(TicketStatus.InProgress, Operation.Start, TicketStatus.InProgress, InvalidTransition)]
    [InlineData(TicketStatus.InProgress, Operation.Resolve, TicketStatus.Resolved, null)]
    [InlineData(TicketStatus.InProgress, Operation.Reopen, TicketStatus.InProgress, InvalidTransition)]
    [InlineData(TicketStatus.InProgress, Operation.Close, TicketStatus.InProgress, InvalidTransition)]
    [InlineData(TicketStatus.InProgress, Operation.Unassign, TicketStatus.New, null)]
    [InlineData(TicketStatus.InProgress, Operation.Recategorize, TicketStatus.InProgress, null)]
    [InlineData(TicketStatus.InProgress, Operation.ChangeImpactAndUrgency, TicketStatus.InProgress, null)]
    [InlineData(TicketStatus.Resolved, Operation.AssignTo, TicketStatus.Resolved, null)]
    [InlineData(TicketStatus.Resolved, Operation.Start, TicketStatus.Resolved, InvalidTransition)]
    [InlineData(TicketStatus.Resolved, Operation.Resolve, TicketStatus.Resolved, InvalidTransition)]
    [InlineData(TicketStatus.Resolved, Operation.Reopen, TicketStatus.InProgress, null)]
    [InlineData(TicketStatus.Resolved, Operation.Close, TicketStatus.Closed, null)]
    [InlineData(TicketStatus.Resolved, Operation.Unassign, TicketStatus.Resolved, InvalidTransition)]
    [InlineData(TicketStatus.Resolved, Operation.Recategorize, TicketStatus.Resolved, RecategorizationNotAllowed)]
    [InlineData(TicketStatus.Resolved, Operation.ChangeImpactAndUrgency, TicketStatus.Resolved, null)]
    [InlineData(TicketStatus.Closed, Operation.AssignTo, TicketStatus.Closed, ClosedCannotBeAssigned)]
    [InlineData(TicketStatus.Closed, Operation.Start, TicketStatus.Closed, InvalidTransition)]
    [InlineData(TicketStatus.Closed, Operation.Resolve, TicketStatus.Closed, InvalidTransition)]
    [InlineData(TicketStatus.Closed, Operation.Reopen, TicketStatus.Closed, InvalidTransition)]
    [InlineData(TicketStatus.Closed, Operation.Close, TicketStatus.Closed, InvalidTransition)]
    [InlineData(TicketStatus.Closed, Operation.Unassign, TicketStatus.Closed, InvalidTransition)]
    [InlineData(TicketStatus.Closed, Operation.Recategorize, TicketStatus.Closed, ClosedCannotBeModified)]
    [InlineData(TicketStatus.Closed, Operation.ChangeImpactAndUrgency, TicketStatus.Closed, ClosedCannotBeModified)]
    public void Operation_InEveryStatus_ReachesExpectedStatusOrFails(
        TicketStatus status,
        Operation operation,
        TicketStatus expectedStatus,
        string? expectedErrorCode)
    {
        var category = TestData.CreateCategory("HARDWARE");
        var otherCategory = TestData.CreateCategory("NETWORK");
        var ticket = TestData.CreateTicketInStatus(status, category, TestData.CreateAgent(category));
        var otherAgent = TestData.CreateAgent(category);
        var historyCountBefore = ticket.History.Count;

        var result = operation switch
        {
            Operation.AssignTo => ticket.AssignTo(otherAgent, TestData.AdminUserId, TestData.Later),
            Operation.Start => ticket.Start(TestData.AdminUserId, TestData.Later),
            Operation.Resolve => ticket.Resolve(TestData.AdminUserId, TestData.Later),
            Operation.Reopen => ticket.Reopen(TestData.AdminUserId, TestData.Later),
            Operation.Close => ticket.Close(TestData.AdminUserId, TestData.Later),
            Operation.Unassign => ticket.Unassign(TestData.Later),
            Operation.Recategorize => ticket.Recategorize(otherCategory, TestData.AdminUserId, TestData.Later),
            Operation.ChangeImpactAndUrgency =>
                ticket.ChangeImpactAndUrgency(Impact.High, Urgency.High, TestData.AdminUserId, TestData.Later),
            _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown operation."),
        };

        ticket.Status.ShouldBe(expectedStatus);
        if (expectedErrorCode is null)
        {
            result.IsSuccess.ShouldBeTrue();
            ticket.History.Count.ShouldBeGreaterThan(historyCountBefore);
        }
        else
        {
            result.Error.Code.ShouldBe(expectedErrorCode);
            ticket.History.Count.ShouldBe(historyCountBefore);
        }
    }
}
