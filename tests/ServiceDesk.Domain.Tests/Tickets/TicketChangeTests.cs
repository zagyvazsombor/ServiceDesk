using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Tickets;
using Shouldly;

namespace ServiceDesk.Domain.Tests.Tickets;

public sealed class TicketChangeTests
{
    private readonly Category _category = TestData.CreateCategory("HARDWARE");
    private readonly Category _otherCategory = TestData.CreateCategory("NETWORK");

    [Fact]
    [Trait("BR", "BR-11")]
    public void ChangeImpactAndUrgency_NewValues_RecalculatesPriority()
    {
        var ticket = TestData.CreateTicket(_category, Impact.Medium, Urgency.Medium);

        var result = ticket.ChangeImpactAndUrgency(Impact.High, Urgency.High, TestData.AdminUserId, TestData.Later);

        result.IsSuccess.ShouldBeTrue();
        ticket.Impact.ShouldBe(Impact.High);
        ticket.Urgency.ShouldBe(Urgency.High);
        ticket.Priority.ShouldBe(Priority.Critical);
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public void ChangeImpactAndUrgency_UndefinedUrgency_ReturnsUrgencyInvalidAndKeepsPriority()
    {
        var ticket = TestData.CreateTicket(_category, Impact.Medium, Urgency.Medium);

        var result = ticket.ChangeImpactAndUrgency(Impact.High, (Urgency)99, TestData.AdminUserId, TestData.Later);

        result.Error.ShouldBe(TicketErrors.UrgencyInvalid);
        ticket.Impact.ShouldBe(Impact.Medium);
        ticket.Priority.ShouldBe(Priority.Medium);
    }

    [Fact]
    [Trait("BR", "BR-11")]
    public void ChangeImpactAndUrgency_ResolvedTicket_RecalculatesPriority()
    {
        var ticket = TestData.CreateTicketInStatus(TicketStatus.Resolved, _category, TestData.CreateAgent(_category));

        var result = ticket.ChangeImpactAndUrgency(Impact.Low, Urgency.Low, TestData.AdminUserId, TestData.Later);

        result.IsSuccess.ShouldBeTrue();
        ticket.Priority.ShouldBe(Priority.Planning);
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public void ChangeImpactAndUrgency_ClosedTicket_ReturnsClosedCannotBeModifiedAndKeepsValues()
    {
        var ticket = TestData.CreateTicketInStatus(TicketStatus.Closed, _category, TestData.CreateAgent(_category));

        var result = ticket.ChangeImpactAndUrgency(Impact.High, Urgency.High, TestData.AdminUserId, TestData.Later);

        result.Error.ShouldBe(TicketErrors.ClosedCannotBeModified);
        ticket.Impact.ShouldBe(Impact.Medium);
        ticket.Urgency.ShouldBe(Urgency.Medium);
        ticket.Priority.ShouldBe(Priority.Medium);
    }

    [Theory]
    [Trait("BR", "BR-07")]
    [Trait("BR", "BR-13")]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    public void Recategorize_ActiveCategoryInAllowedStatus_ChangesCategoryAndKeepsAssignment(TicketStatus status)
    {
        var ticket = TestData.CreateTicketInStatus(status, _category, TestData.CreateAgent(_category));
        var assigneeBefore = ticket.AssigneeAgentId;

        var result = ticket.Recategorize(_otherCategory, TestData.AdminUserId, TestData.Later);

        result.IsSuccess.ShouldBeTrue();
        ticket.CategoryId.ShouldBe(_otherCategory.Id);
        ticket.AssigneeAgentId.ShouldBe(assigneeBefore);
        ticket.Status.ShouldBe(status);
    }

    [Fact]
    [Trait("BR", "BR-07")]
    public void Recategorize_ResolvedTicket_ReturnsRecategorizationNotAllowed()
    {
        var ticket = TestData.CreateTicketInStatus(TicketStatus.Resolved, _category, TestData.CreateAgent(_category));

        var result = ticket.Recategorize(_otherCategory, TestData.AdminUserId, TestData.Later);

        result.Error.ShouldBe(TicketErrors.RecategorizationNotAllowed);
        ticket.CategoryId.ShouldBe(_category.Id);
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public void Recategorize_ClosedTicket_ReturnsClosedCannotBeModified()
    {
        var ticket = TestData.CreateTicketInStatus(TicketStatus.Closed, _category, TestData.CreateAgent(_category));

        var result = ticket.Recategorize(_otherCategory, TestData.AdminUserId, TestData.Later);

        result.Error.ShouldBe(TicketErrors.ClosedCannotBeModified);
        ticket.CategoryId.ShouldBe(_category.Id);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public void Recategorize_InactiveCategory_ReturnsCategoryInactiveError()
    {
        var ticket = TestData.CreateTicket(_category);
        var inactiveCategory = TestData.CreateInactiveCategory();

        var result = ticket.Recategorize(inactiveCategory, TestData.AdminUserId, TestData.Later);

        result.Error.ShouldBe(CategoryErrors.Inactive);
        ticket.CategoryId.ShouldBe(_category.Id);
    }

    [Theory]
    [Trait("BR", "BR-07")]
    [InlineData(TicketStatus.Assigned)]
    [InlineData(TicketStatus.InProgress)]
    public void Unassign_OpenAssignedTicket_ClearsAssigneeAndReturnsToNew(TicketStatus status)
    {
        var ticket = TestData.CreateTicketInStatus(status, _category, TestData.CreateAgent(_category));

        var result = ticket.Unassign(TestData.Later);

        result.IsSuccess.ShouldBeTrue();
        ticket.AssigneeAgentId.ShouldBeNull();
        ticket.Status.ShouldBe(TicketStatus.New);
    }

    [Theory]
    [Trait("BR", "BR-07")]
    [InlineData(TicketStatus.New)]
    [InlineData(TicketStatus.Resolved)]
    [InlineData(TicketStatus.Closed)]
    public void Unassign_TicketWithoutOpenAssignment_ReturnsInvalidTransition(TicketStatus status)
    {
        var ticket = TestData.CreateTicketInStatus(status, _category, TestData.CreateAgent(_category));
        var assigneeBefore = ticket.AssigneeAgentId;

        var result = ticket.Unassign(TestData.Later);

        result.Error.ShouldBe(TicketErrors.InvalidTransition);
        ticket.AssigneeAgentId.ShouldBe(assigneeBefore);
        ticket.Status.ShouldBe(status);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Start_MissingActorUserId_ThrowsArgumentException(string actorUserId)
    {
        var ticket = TestData.CreateTicketInStatus(TicketStatus.Assigned, _category, TestData.CreateAgent(_category));

        Should.Throw<ArgumentException>(() => ticket.Start(actorUserId, TestData.Later));
    }
}
