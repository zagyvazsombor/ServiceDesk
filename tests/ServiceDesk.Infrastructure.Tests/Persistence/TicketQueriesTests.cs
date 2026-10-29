using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Common.Paging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using ServiceDesk.Application.Tickets.GetMyTickets;
using ServiceDesk.Application.Tickets.GetTicketDetails;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Infrastructure.Tests.Builders;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Persistence;

public sealed class TicketQueriesTests(DatabaseFixture database) : IntegrationTest(database)
{
    [Fact]
    [Trait("BR", "BR-02")]
    public async Task GetMyTicketsAsync_TicketsOfTwoReporters_ReturnsOnlyOwnNewestFirst()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var otherReporter = new UserBuilder().Build();
        var hardware = new CategoryBuilder().WithName("Hardver").Build();
        var oldest = new TicketBuilder(reporter, hardware).CreatedAt(TestTime.Now).Build();
        var newest = new TicketBuilder(reporter, hardware).CreatedAt(TestTime.Now.AddHours(2)).Build();
        var middle = new TicketBuilder(reporter, hardware).CreatedAt(TestTime.Now.AddHours(1)).Build();
        var foreign = new TicketBuilder(otherReporter, hardware).CreatedAt(TestTime.Now.AddHours(3)).Build();
        await SaveAsync(reporter, otherReporter, hardware, oldest, newest, middle, foreign);

        // Act
        var page = await GetMyTicketsAsync(reporter.Id, pageNumber: 1, pageSize: 20);

        // Assert
        page.Items.Select(ticket => ticket.Id).ShouldBe([newest.Id, middle.Id, oldest.Id]);
        page.TotalCount.ShouldBe(3);
        page.Items[0].ShouldBe(new MyTicketDto(
            newest.Id, newest.Title, "Hardver", null, newest.Priority, TicketStatus.New, newest.CreatedAt));
    }

    [Fact]
    public async Task GetMyTicketsAsync_AssignedAndNewTicket_ReturnsAssigneeNameOnlyForAssigned()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var annaUser = new UserBuilder().Build();
        var hardware = new CategoryBuilder().Build();
        var anna = new AgentBuilder(annaUser).WithDisplayName("Kiss Anna").WithCompetencies(hardware).Build();
        var assigned = new TicketBuilder(reporter, hardware)
            .CreatedAt(TestTime.Now.AddHours(1))
            .InStatus(TicketStatus.Assigned, anna, annaUser)
            .Build();
        var unassigned = new TicketBuilder(reporter, hardware).CreatedAt(TestTime.Now).Build();
        await SaveAsync(reporter, annaUser, hardware, anna, assigned, unassigned);

        // Act
        var page = await GetMyTicketsAsync(reporter.Id, pageNumber: 1, pageSize: 20);

        // Assert
        page.Items.Select(ticket => (ticket.Id, ticket.Status, ticket.AssigneeName)).ShouldBe(
        [
            (assigned.Id, TicketStatus.Assigned, "Kiss Anna"),
            (unassigned.Id, TicketStatus.New, null),
        ]);
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public async Task GetMyTicketsAsync_SecondPage_ReturnsRemainingTicketsWithTotalCount()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var hardware = new CategoryBuilder().Build();
        var tickets = Enumerable.Range(0, 3)
            .Select(hour => new TicketBuilder(reporter, hardware).CreatedAt(TestTime.Now.AddHours(hour)).Build())
            .ToArray();
        await SaveAsync([reporter, hardware, .. tickets]);

        // Act
        var page = await GetMyTicketsAsync(reporter.Id, pageNumber: 2, pageSize: 2);

        // Assert: the oldest ticket is the only one left for the second page.
        page.Items.Select(ticket => ticket.Id).ShouldBe([tickets[0].Id]);
        page.PageNumber.ShouldBe(2);
        page.PageSize.ShouldBe(2);
        page.TotalCount.ShouldBe(3);
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public async Task GetMyTicketsAsync_PagePastTheLast_ReturnsNoItemsWithTotalCount()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var hardware = new CategoryBuilder().Build();
        var ticket = new TicketBuilder(reporter, hardware).Build();
        await SaveAsync(reporter, hardware, ticket);

        // Act
        var page = await GetMyTicketsAsync(reporter.Id, pageNumber: 2, pageSize: 20);

        // Assert
        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(1);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task GetActiveCategoriesAsync_ActiveAndInactiveCategories_ReturnsOnlyActiveByName()
    {
        // Arrange
        var software = new CategoryBuilder().WithCode("SOFTWARE").WithName("Szoftver").Build();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").WithName("Hardver").Build();
        var other = new CategoryBuilder().WithCode("OTHER").WithName("Egyeb").Build();
        other.Deactivate();
        await SaveAsync(software, hardware, other);

        // Act
        IReadOnlyList<ActiveCategoryDto> categories;
        await using (var scope = Database.CreateScope())
        {
            categories = await scope.ServiceProvider.GetRequiredService<ITicketQueries>()
                .GetActiveCategoriesAsync(CancellationToken);
        }

        // Assert
        categories.ShouldBe(
        [
            new ActiveCategoryDto(hardware.Id, "HARDWARE", "Hardver", hardware.Description),
            new ActiveCategoryDto(software.Id, "SOFTWARE", "Szoftver", software.Description),
        ]);
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public async Task GetTicketDetailsAsync_ForeignTicketWithReporterFilter_ReturnsNull()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var otherReporter = new UserBuilder().Build();
        var hardware = new CategoryBuilder().Build();
        var foreignTicket = new TicketBuilder(otherReporter, hardware).Build();
        await SaveAsync(reporter, otherReporter, hardware, foreignTicket);

        // Act
        var details = await GetTicketDetailsAsync(foreignTicket.Id, reporter.Id);

        // Assert
        details.ShouldBeNull();
    }

    [Fact]
    public async Task GetTicketDetailsAsync_UnknownTicket_ReturnsNull()
    {
        // Act
        var details = await GetTicketDetailsAsync(Guid.CreateVersion7(), reporterId: null);

        // Assert
        details.ShouldBeNull();
    }

    [Fact]
    [Trait("BR", "BR-02")]
    public async Task GetTicketDetailsAsync_OwnNewTicketWithReporterFilter_ReturnsDetailsWithoutAssignee()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").WithName("Hardver").Build();
        var ticket = new TicketBuilder(reporter, hardware).WithImpactAndUrgency(Impact.High, Urgency.Low).Build();
        await SaveAsync(reporter, hardware, ticket);

        // Act
        var details = await GetTicketDetailsAsync(ticket.Id, reporter.Id);

        // Assert
        details.ShouldNotBeNull();
        details.Id.ShouldBe(ticket.Id);
        details.Title.ShouldBe(ticket.Title);
        details.Description.ShouldBe(ticket.Description);
        details.Impact.ShouldBe(Impact.High);
        details.Urgency.ShouldBe(Urgency.Low);
        details.Priority.ShouldBe(Priority.Medium); // BR-11: High × Low
        details.Status.ShouldBe(TicketStatus.New);
        details.CreatedAt.ShouldBe(TestTime.Now);
        details.ReporterEmail.ShouldBe(reporter.Email);
        details.CategoryCode.ShouldBe("HARDWARE");
        details.CategoryName.ShouldBe("Hardver");
        details.AssigneeName.ShouldBeNull();
        details.History.ShouldBe(
        [
            new TicketHistoryEntryDto(1, TestTime.Now, TicketHistoryAction.Created, reporter.Email, null, nameof(TicketStatus.New)),
        ]);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public async Task GetTicketDetailsAsync_ReassignedAndRecategorizedTicket_ResolvesNamesInHistoryOrder()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var annaUser = new UserBuilder().Build();
        var belaUser = new UserBuilder().Build();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").WithName("Hardver").Build();
        var network = new CategoryBuilder().WithCode("NETWORK").WithName("Halozat").Build();
        var anna = new AgentBuilder(annaUser).WithDisplayName("Kiss Anna").WithCompetencies(hardware, network).Build();
        var bela = new AgentBuilder(belaUser).WithDisplayName("Nagy Béla").WithCompetencies(hardware, network).Build();
        // Assigned to Anna by the system, started by Anna, then reassigned to Béla and recategorized by Anna.
        var ticket = new TicketBuilder(reporter, hardware).InStatus(TicketStatus.InProgress, anna, annaUser).Build();
        ticket.AssignTo(bela, annaUser.Id, TestTime.Now.AddHours(1)).EnsureSuccess();
        ticket.Recategorize(network, annaUser.Id, TestTime.Now.AddHours(2)).EnsureSuccess();
        await SaveAsync(reporter, annaUser, belaUser, hardware, network, anna, bela, ticket);

        // Act
        var details = await GetTicketDetailsAsync(ticket.Id, reporterId: null);

        // Assert
        details.ShouldNotBeNull();
        details.Status.ShouldBe(TicketStatus.InProgress);
        details.CategoryCode.ShouldBe("NETWORK");
        details.CategoryName.ShouldBe("Halozat");
        details.AssigneeName.ShouldBe("Nagy Béla");
        details.History
            .Select(entry => (entry.Sequence, entry.Action, entry.ActorName, entry.OldValue, entry.NewValue))
            .ShouldBe(
            [
                (1, TicketHistoryAction.Created, reporter.Email, null, "New"),
                (2, TicketHistoryAction.Assigned, null, null, "Kiss Anna"),
                (3, TicketHistoryAction.StatusChanged, null, "New", "Assigned"),
                (4, TicketHistoryAction.StatusChanged, "Kiss Anna", "Assigned", "InProgress"),
                (5, TicketHistoryAction.Reassigned, "Kiss Anna", "Kiss Anna", "Nagy Béla"),
                (6, TicketHistoryAction.Recategorized, "Kiss Anna", "Hardver", "Halozat"),
            ]);
        details.History[^1].At.ShouldBe(TestTime.Now.AddHours(2));
    }

    private async Task<PagedResult<MyTicketDto>> GetMyTicketsAsync(string reporterId, int pageNumber, int pageSize)
    {
        await using var scope = Database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ITicketQueries>()
            .GetMyTicketsAsync(reporterId, pageNumber, pageSize, CancellationToken);
    }

    private async Task<TicketDetailsDto?> GetTicketDetailsAsync(Guid ticketId, string? reporterId)
    {
        await using var scope = Database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ITicketQueries>()
            .GetTicketDetailsAsync(ticketId, reporterId, CancellationToken);
    }
}
