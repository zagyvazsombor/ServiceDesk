using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Domain.Tests.Tickets;

public sealed class TicketHistoryTests
{
    private readonly Category _category = TestData.CreateCategory("HARDWARE");
    private readonly Category _otherCategory = TestData.CreateCategory("NETWORK");

    [Fact]
    [Trait("BR", "BR-10")]
    public void Create_ValidInput_AddsCreatedEntryByReporter()
    {
        var ticket = TestData.CreateTicket(_category);

        var entry = ticket.History.ShouldHaveSingleItem();
        entry.Id.ShouldNotBe(Guid.Empty);
        entry.At.ShouldBe(TestData.Now);
        Snapshot(entry).ShouldBe(new EntrySnapshot(TicketHistoryAction.Created, TestData.ReporterId, null, "New"));
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void AssignTo_NewTicketBySystem_AddsAssignedAndStatusChangedEntries()
    {
        var ticket = TestData.CreateTicket(_category);
        var agent = TestData.CreateAgent(_category);
        var countBefore = ticket.History.Count;

        ticket.AssignTo(agent, null, TestData.Later);

        NewEntries(ticket, countBefore).ShouldBe(
        [
            new EntrySnapshot(TicketHistoryAction.Assigned, null, null, agent.Id.ToString()),
            new EntrySnapshot(TicketHistoryAction.StatusChanged, null, "New", "Assigned"),
        ]);
        ticket.History.Last().At.ShouldBe(TestData.Later);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void AssignTo_AssignedTicketByAdmin_AddsReassignedEntry()
    {
        var agent = TestData.CreateAgent(_category);
        var ticket = TestData.CreateTicketInStatus(TicketStatus.Assigned, _category, agent);
        var otherAgent = TestData.CreateAgent(_category);
        var countBefore = ticket.History.Count;

        ticket.AssignTo(otherAgent, TestData.AdminUserId, TestData.Later);

        NewEntries(ticket, countBefore).ShouldBe(
        [
            new EntrySnapshot(
                TicketHistoryAction.Reassigned,
                TestData.AdminUserId,
                agent.Id.ToString(),
                otherAgent.Id.ToString()),
        ]);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void Start_AssignedTicket_AddsStatusChangedEntry()
    {
        var ticket = TestData.CreateTicketInStatus(TicketStatus.Assigned, _category, TestData.CreateAgent(_category));
        var countBefore = ticket.History.Count;

        ticket.Start(TestData.AdminUserId, TestData.Later);

        NewEntries(ticket, countBefore).ShouldBe(
        [
            new EntrySnapshot(TicketHistoryAction.StatusChanged, TestData.AdminUserId, "Assigned", "InProgress"),
        ]);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void Recategorize_DifferentCategory_AddsRecategorizedEntry()
    {
        var ticket = TestData.CreateTicket(_category);
        var countBefore = ticket.History.Count;

        ticket.Recategorize(_otherCategory, TestData.AdminUserId, TestData.Later);

        NewEntries(ticket, countBefore).ShouldBe(
        [
            new EntrySnapshot(
                TicketHistoryAction.Recategorized,
                TestData.AdminUserId,
                _category.Id.ToString(),
                _otherCategory.Id.ToString()),
        ]);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void Recategorize_SameCategory_AddsNoEntry()
    {
        var ticket = TestData.CreateTicket(_category);
        var countBefore = ticket.History.Count;

        var result = ticket.Recategorize(_category, TestData.AdminUserId, TestData.Later);

        result.IsSuccess.ShouldBeTrue();
        ticket.History.Count.ShouldBe(countBefore);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void ChangeImpactAndUrgency_BothChanged_AddsImpactAndUrgencyEntries()
    {
        var ticket = TestData.CreateTicket(_category, Impact.Medium, Urgency.Medium);
        var countBefore = ticket.History.Count;

        ticket.ChangeImpactAndUrgency(Impact.High, Urgency.Low, TestData.AdminUserId, TestData.Later);

        NewEntries(ticket, countBefore).ShouldBe(
        [
            new EntrySnapshot(TicketHistoryAction.ImpactChanged, TestData.AdminUserId, "Medium", "High"),
            new EntrySnapshot(TicketHistoryAction.UrgencyChanged, TestData.AdminUserId, "Medium", "Low"),
        ]);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void ChangeImpactAndUrgency_OnlyUrgencyChanged_AddsUrgencyEntryOnly()
    {
        var ticket = TestData.CreateTicket(_category, Impact.Medium, Urgency.Medium);
        var countBefore = ticket.History.Count;

        ticket.ChangeImpactAndUrgency(Impact.Medium, Urgency.High, TestData.AdminUserId, TestData.Later);

        NewEntries(ticket, countBefore).ShouldBe(
        [
            new EntrySnapshot(TicketHistoryAction.UrgencyChanged, TestData.AdminUserId, "Medium", "High"),
        ]);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void ChangeImpactAndUrgency_NothingChanged_AddsNoEntry()
    {
        var ticket = TestData.CreateTicket(_category, Impact.Medium, Urgency.Medium);
        var countBefore = ticket.History.Count;

        var result = ticket.ChangeImpactAndUrgency(Impact.Medium, Urgency.Medium, TestData.AdminUserId, TestData.Later);

        result.IsSuccess.ShouldBeTrue();
        ticket.History.Count.ShouldBe(countBefore);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void Unassign_InProgressTicket_AddsSystemEntries()
    {
        var agent = TestData.CreateAgent(_category);
        var ticket = TestData.CreateTicketInStatus(TicketStatus.InProgress, _category, agent);
        var countBefore = ticket.History.Count;

        ticket.Unassign(TestData.Later);

        NewEntries(ticket, countBefore).ShouldBe(
        [
            new EntrySnapshot(TicketHistoryAction.Unassigned, null, agent.Id.ToString(), null),
            new EntrySnapshot(TicketHistoryAction.StatusChanged, null, "InProgress", "New"),
        ]);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void Start_NewTicket_FailsWithoutEntry()
    {
        var ticket = TestData.CreateTicket(_category);
        var countBefore = ticket.History.Count;

        var result = ticket.Start(TestData.AdminUserId, TestData.Later);

        result.IsFailure.ShouldBeTrue();
        ticket.History.Count.ShouldBe(countBefore);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void Create_ValidInput_FirstEntryHasSequenceOne()
    {
        var ticket = TestData.CreateTicket(_category);

        ticket.History.ShouldHaveSingleItem().Sequence.ShouldBe(1);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void AssignTo_NewTicket_NumbersEntriesOfOneOperationInOrder()
    {
        var ticket = TestData.CreateTicket(_category);

        ticket.AssignTo(TestData.CreateAgent(_category), null, TestData.Later);

        ticket.History.OrderBy(entry => entry.Sequence).Select(entry => (entry.Sequence, entry.Action)).ShouldBe(
        [
            (1, TicketHistoryAction.Created),
            (2, TicketHistoryAction.Assigned),
            (3, TicketHistoryAction.StatusChanged),
        ]);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void ChangeImpactAndUrgency_BothChanged_NumbersImpactBeforeUrgency()
    {
        var ticket = TestData.CreateTicket(_category, Impact.Medium, Urgency.Medium);

        ticket.ChangeImpactAndUrgency(Impact.High, Urgency.Low, TestData.AdminUserId, TestData.Later);

        ticket.History.OrderBy(entry => entry.Sequence).Select(entry => (entry.Sequence, entry.Action)).ShouldBe(
        [
            (1, TicketHistoryAction.Created),
            (2, TicketHistoryAction.ImpactChanged),
            (3, TicketHistoryAction.UrgencyChanged),
        ]);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void History_MainPathToClosed_SequenceRunsFromOneWithoutGaps()
    {
        var ticket = TestData.CreateTicketInStatus(TicketStatus.Closed, _category, TestData.CreateAgent(_category));

        ticket.History.Select(entry => entry.Sequence).ShouldBe(Enumerable.Range(1, ticket.History.Count));
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public void AssignTo_AfterFailedOperation_ContinuesSequenceWithoutGap()
    {
        var ticket = TestData.CreateTicket(_category);
        ticket.Start(TestData.AdminUserId, TestData.Later);

        ticket.AssignTo(TestData.CreateAgent(_category), null, TestData.Later);

        ticket.History.Select(entry => entry.Sequence).ShouldBe([1, 2, 3]);
    }

    private static EntrySnapshot Snapshot(TicketHistoryEntry entry) =>
        new(entry.Action, entry.ActorUserId, entry.OldValue, entry.NewValue);

    private static List<EntrySnapshot> NewEntries(Ticket ticket, int countBefore) =>
        ticket.History.Skip(countBefore).Select(Snapshot).ToList();

    private sealed record EntrySnapshot(
        TicketHistoryAction Action,
        string? ActorUserId,
        string? OldValue,
        string? NewValue);
}
