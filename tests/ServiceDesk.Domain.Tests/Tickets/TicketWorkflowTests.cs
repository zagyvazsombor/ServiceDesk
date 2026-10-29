using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Domain.Tests.Tickets;

// The allowed operations in every status (AGENTS.md §10); TicketStateMachineTests checks that Ticket enforces them.
public sealed class TicketWorkflowTests
{
    public enum Operation
    {
        Start,
        Resolve,
        Reopen,
        Close,
        Unassign,
        Assign,
        ChangeImpactAndUrgency,
        Recategorize,
        RetryAutoAssign,
    }

    public static TheoryData<TicketStatus, Operation[]> AllowedOperations => new()
    {
        {
            TicketStatus.New,
            [Operation.Assign, Operation.ChangeImpactAndUrgency, Operation.Recategorize, Operation.RetryAutoAssign]
        },
        {
            TicketStatus.Assigned,
            [Operation.Start, Operation.Unassign, Operation.Assign, Operation.ChangeImpactAndUrgency, Operation.Recategorize]
        },
        {
            TicketStatus.InProgress,
            [Operation.Resolve, Operation.Unassign, Operation.Assign, Operation.ChangeImpactAndUrgency, Operation.Recategorize]
        },
        {
            TicketStatus.Resolved,
            [Operation.Reopen, Operation.Close, Operation.Assign, Operation.ChangeImpactAndUrgency]
        },
        { TicketStatus.Closed, [] },
    };

    [Theory]
    [Trait("BR", "BR-03")]
    [Trait("BR", "BR-06")]
    [Trait("BR", "BR-07")]
    [MemberData(nameof(AllowedOperations))]
    public void Can_EveryOperationInStatus_IsAllowedOnlyByStateMachine(TicketStatus status, Operation[] allowed)
    {
        var actuallyAllowed = Enum.GetValues<Operation>().Where(operation => IsAllowed(operation, status));

        actuallyAllowed.ShouldBe(allowed);
    }

    [Theory]
    [Trait("BR", "BR-03")]
    [InlineData(TicketStatus.New, false)]
    [InlineData(TicketStatus.Assigned, false)]
    [InlineData(TicketStatus.InProgress, false)]
    [InlineData(TicketStatus.Resolved, false)]
    [InlineData(TicketStatus.Closed, true)]
    public void IsTerminal_Status_IsTrueOnlyForClosed(TicketStatus status, bool expected)
    {
        TicketWorkflow.IsTerminal(status).ShouldBe(expected);
    }

    private static bool IsAllowed(Operation operation, TicketStatus status) => operation switch
    {
        Operation.Start => TicketWorkflow.CanStart(status),
        Operation.Resolve => TicketWorkflow.CanResolve(status),
        Operation.Reopen => TicketWorkflow.CanReopen(status),
        Operation.Close => TicketWorkflow.CanClose(status),
        Operation.Unassign => TicketWorkflow.CanUnassign(status),
        Operation.Assign => TicketWorkflow.CanAssign(status),
        Operation.ChangeImpactAndUrgency => TicketWorkflow.CanChangeImpactAndUrgency(status),
        Operation.Recategorize => TicketWorkflow.CanRecategorize(status),
        Operation.RetryAutoAssign => TicketWorkflow.CanRetryAutoAssign(status),
        _ => throw new ArgumentOutOfRangeException(nameof(operation), operation, "Unknown operation."),
    };
}
