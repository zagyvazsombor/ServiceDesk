using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Agents.ListAgents;
using ServiceDesk.Application.Categories.ListCategories;
using ServiceDesk.Application.Common.Paging;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using ServiceDesk.Application.Tickets.GetAssignableAgents;
using ServiceDesk.Application.Tickets.GetMyTickets;
using ServiceDesk.Application.Tickets.GetTicketDetails;
using ServiceDesk.Application.Tickets.GetTicketQueue;
using ServiceDesk.Application.Tickets.GetTicketQueueFilters;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Infrastructure.Tests.Builders;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Persistence;

public sealed class TicketQueriesTests(DatabaseFixture database) : IntegrationTest(database)
{
    public enum QueueFilter
    {
        Status,
        Category,
        Priority,
        Assignee,
        Unassigned,
        CategoryAndAssignee,
        IncludeClosed,
        ClosedStatus,
    }

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

    [Fact]
    public async Task GetTicketQueueAsync_TicketsOfEveryPriority_OrdersByPriorityThenOldestFirst()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var hardware = new CategoryBuilder().WithName("Hardver").Build();
        TicketBuilder Ticket(Impact impact, Urgency urgency, int hour) =>
            new TicketBuilder(reporter, hardware).WithImpactAndUrgency(impact, urgency).CreatedAt(TestTime.Now.AddHours(hour));
        var planning = Ticket(Impact.Low, Urgency.Low, hour: 0).Build();
        var low = Ticket(Impact.Medium, Urgency.Low, hour: 1).Build();
        var newerMedium = Ticket(Impact.Medium, Urgency.Medium, hour: 2).Build();
        var olderMedium = Ticket(Impact.Low, Urgency.High, hour: 0).Build();
        var high = Ticket(Impact.High, Urgency.Medium, hour: 4).Build();
        var critical = Ticket(Impact.High, Urgency.High, hour: 5).Build();
        await SaveAsync(reporter, hardware, planning, low, newerMedium, olderMedium, high, critical);

        // Act
        var page = await GetTicketQueueAsync(new GetTicketQueueQuery(), pageSize: 20);

        // Assert
        page.Items.Select(ticket => ticket.Id).ShouldBe(
            [critical.Id, high.Id, olderMedium.Id, newerMedium.Id, low.Id, planning.Id]);
        page.TotalCount.ShouldBe(6);
        page.Items[0].ShouldBe(new TicketQueueItemDto(
            critical.Id,
            critical.Title,
            "Hardver",
            Priority.Critical,
            TicketStatus.New,
            null,
            reporter.Email,
            critical.CreatedAt));
    }

    [Theory]
    [InlineData(QueueFilter.Status)]
    [InlineData(QueueFilter.Category)]
    [InlineData(QueueFilter.Priority)]
    [InlineData(QueueFilter.Assignee)]
    [InlineData(QueueFilter.Unassigned)]
    [InlineData(QueueFilter.CategoryAndAssignee)]
    [InlineData(QueueFilter.IncludeClosed)]
    [InlineData(QueueFilter.ClosedStatus)]
    public async Task GetTicketQueueAsync_Filter_ReturnsOnlyMatchingTickets(QueueFilter filter)
    {
        // Arrange
        var scenario = await SaveQueueScenarioAsync();
        var (query, expected) = filter switch
        {
            QueueFilter.Status => (
                new GetTicketQueueQuery(Status: TicketStatus.InProgress),
                new[] { scenario.NetworkInProgressOfBela }),
            QueueFilter.Category => (
                new GetTicketQueueQuery(CategoryId: scenario.Network.Id, IncludeClosed: true),
                [scenario.NetworkInProgressOfBela, scenario.NetworkClosedOfAnna]),
            QueueFilter.Priority => (
                new GetTicketQueueQuery(Priority: Priority.Medium),
                [scenario.HardwareNew, scenario.NetworkInProgressOfBela]),
            QueueFilter.Assignee => (
                new GetTicketQueueQuery(AssigneeAgentId: scenario.Anna.Id, IncludeClosed: true),
                [scenario.HardwareAssignedToAnna, scenario.NetworkClosedOfAnna]),
            QueueFilter.Unassigned => (
                new GetTicketQueueQuery(UnassignedOnly: true),
                [scenario.HardwareNew]),
            QueueFilter.CategoryAndAssignee => (
                new GetTicketQueueQuery(CategoryId: scenario.Network.Id, AssigneeAgentId: scenario.Anna.Id, IncludeClosed: true),
                [scenario.NetworkClosedOfAnna]),
            QueueFilter.IncludeClosed => (
                new GetTicketQueueQuery(IncludeClosed: true),
                [
                    scenario.HardwareAssignedToAnna,
                    scenario.HardwareNew,
                    scenario.NetworkInProgressOfBela,
                    scenario.NetworkClosedOfAnna,
                ]),
            QueueFilter.ClosedStatus => (
                new GetTicketQueueQuery(Status: TicketStatus.Closed, IncludeClosed: true),
                [scenario.NetworkClosedOfAnna]),
            _ => throw new ArgumentOutOfRangeException(nameof(filter), filter, "Unknown filter."),
        };

        // Act
        var page = await GetTicketQueueAsync(query, pageSize: 20);

        // Assert
        page.Items.Select(ticket => ticket.Id).ShouldBe(expected.Select(ticket => ticket.Id));
        page.TotalCount.ShouldBe(expected.Length);
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task GetTicketQueueAsync_DefaultQuery_LeavesOutClosedTickets()
    {
        // Arrange
        var scenario = await SaveQueueScenarioAsync();

        // Act
        var page = await GetTicketQueueAsync(new GetTicketQueueQuery(), pageSize: 20);

        // Assert: Anna's closed NETWORK ticket is the only one missing; High first, then the older Medium ticket.
        page.Items.Select(ticket => ticket.Id).ShouldBe(
            [scenario.HardwareAssignedToAnna.Id, scenario.HardwareNew.Id, scenario.NetworkInProgressOfBela.Id]);
        page.TotalCount.ShouldBe(3);
    }

    [Fact]
    public async Task GetTicketQueueAsync_AssignedTicket_ReturnsAssigneeName()
    {
        // Arrange
        var scenario = await SaveQueueScenarioAsync();

        // Act
        var page = await GetTicketQueueAsync(new GetTicketQueueQuery(AssigneeAgentId: scenario.Bela.Id), pageSize: 20);

        // Assert
        page.Items.ShouldHaveSingleItem().AssigneeName.ShouldBe("Nagy Béla");
    }

    [Fact]
    public async Task GetTicketQueueAsync_SecondPage_ReturnsRemainingTicketsWithTotalCount()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var hardware = new CategoryBuilder().Build();
        var tickets = Enumerable.Range(0, 3)
            .Select(hour => new TicketBuilder(reporter, hardware).CreatedAt(TestTime.Now.AddHours(hour)).Build())
            .ToArray();
        await SaveAsync([reporter, hardware, .. tickets]);

        // Act
        var page = await GetTicketQueueAsync(new GetTicketQueueQuery(PageNumber: 2), pageSize: 2);

        // Assert: the same priority, so the newest ticket is the only one left for the second page.
        page.Items.Select(ticket => ticket.Id).ShouldBe([tickets[2].Id]);
        page.PageNumber.ShouldBe(2);
        page.PageSize.ShouldBe(2);
        page.TotalCount.ShouldBe(3);
    }

    [Fact]
    [Trait("BR", "BR-04")]
    [Trait("BR", "BR-05")]
    public async Task GetAssignableAgentsAsync_SeveralAgents_ReturnsActiveCompetentAgentsLeastLoadedThenByName()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var users = Enumerable.Range(0, 5).Select(_ => new UserBuilder().Build()).ToArray();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var network = new CategoryBuilder().WithCode("NETWORK").Build();
        var anna = new AgentBuilder(users[0]).WithDisplayName("Kiss Anna").WithCompetencies(hardware).Build();
        var bela = new AgentBuilder(users[1]).WithDisplayName("Nagy Béla").WithCompetencies(hardware, network).Build();
        var csaba = new AgentBuilder(users[2]).WithDisplayName("Tóth Csaba").WithCompetencies(network).Build();
        var denes = new AgentBuilder(users[3]).WithDisplayName("Inaktív Dénes").WithCompetencies(hardware).Inactive().Build();
        var agota = new AgentBuilder(users[4]).WithDisplayName("Bodnár Ágota").WithCompetencies(hardware).Build();
        // Anna: the ticket itself (Medium, 3) is open, her resolved ticket does not count.
        var ticket = new TicketBuilder(reporter, hardware).InStatus(TicketStatus.Assigned, anna, users[0]).Build();
        var resolvedOfAnna = new TicketBuilder(reporter, hardware).InStatus(TicketStatus.Resolved, anna, users[0]).Build();
        // Béla: an InProgress Critical ticket (5) counts, his closed ticket does not.
        var criticalOfBela = new TicketBuilder(reporter, network)
            .WithImpactAndUrgency(Impact.High, Urgency.High)
            .InStatus(TicketStatus.InProgress, bela, users[1])
            .Build();
        var closedOfBela = new TicketBuilder(reporter, network).InStatus(TicketStatus.Closed, bela, users[1]).Build();
        await SaveAsync(
            [reporter, .. users, hardware, network, anna, bela, csaba, denes, agota, ticket, resolvedOfAnna, criticalOfBela, closedOfBela]);

        // Act
        var assignableAgents = await GetAssignableAgentsAsync(ticket.Id);

        // Assert: Csaba has no HARDWARE competency, Dénes is inactive (BR-04).
        assignableAgents.ShouldNotBeNull();
        assignableAgents.TicketStatus.ShouldBe(TicketStatus.Assigned);
        assignableAgents.Agents.ShouldBe(
        [
            new AssignableAgentDto(agota.Id, "Bodnár Ágota", Load: 0, OpenTicketCount: 0, IsCurrentAssignee: false),
            new AssignableAgentDto(anna.Id, "Kiss Anna", Load: 3, OpenTicketCount: 1, IsCurrentAssignee: true),
            new AssignableAgentDto(bela.Id, "Nagy Béla", Load: 5, OpenTicketCount: 1, IsCurrentAssignee: false),
        ]);
    }

    [Fact]
    [Trait("BR", "BR-06")]
    public async Task GetAssignableAgentsAsync_NobodyEligible_ReturnsTicketStatusWithoutAgents()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var hardware = new CategoryBuilder().Build();
        var ticket = new TicketBuilder(reporter, hardware).Build();
        await SaveAsync(reporter, hardware, ticket);

        // Act
        var assignableAgents = await GetAssignableAgentsAsync(ticket.Id);

        // Assert
        assignableAgents.ShouldNotBeNull();
        assignableAgents.TicketStatus.ShouldBe(TicketStatus.New);
        assignableAgents.Agents.ShouldBeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-12")]
    [Trait("BR", "BR-13")]
    public async Task GetTicketQueueFiltersAsync_ActiveAndInactiveEntries_ReturnsEveryCategoryAndAgentByName()
    {
        // Arrange
        var users = Enumerable.Range(0, 3).Select(_ => new UserBuilder().Build()).ToArray();
        var software = new CategoryBuilder().WithCode("SOFTWARE").WithName("Szoftver").Build();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").WithName("Hardver").Build();
        var other = new CategoryBuilder().WithCode("OTHER").WithName("Egyeb").Build();
        other.Deactivate();
        var csaba = new AgentBuilder(users[0]).WithDisplayName("Tóth Csaba").Build();
        var anna = new AgentBuilder(users[1]).WithDisplayName("Kiss Anna").Build();
        var bela = new AgentBuilder(users[2]).WithDisplayName("Nagy Béla").Inactive().Build();
        await SaveAsync([.. users, software, hardware, other, csaba, anna, bela]);

        // Act
        TicketQueueFiltersDto filters;
        await using (var scope = Database.CreateScope())
        {
            filters = await scope.ServiceProvider.GetRequiredService<ITicketQueries>()
                .GetTicketQueueFiltersAsync(CancellationToken);
        }

        // Assert
        filters.Categories.ShouldBe(
        [
            new QueueFilterOptionDto(other.Id, "Egyeb", IsActive: false),
            new QueueFilterOptionDto(hardware.Id, "Hardver", IsActive: true),
            new QueueFilterOptionDto(software.Id, "Szoftver", IsActive: true),
        ]);
        filters.Agents.ShouldBe(
        [
            new QueueFilterOptionDto(anna.Id, "Kiss Anna", IsActive: true),
            new QueueFilterOptionDto(bela.Id, "Nagy Béla", IsActive: false),
            new QueueFilterOptionDto(csaba.Id, "Tóth Csaba", IsActive: true),
        ]);
    }

    [Fact]
    public async Task GetAssignableAgentsAsync_UnknownTicket_ReturnsNull()
    {
        // Act
        var assignableAgents = await GetAssignableAgentsAsync(Guid.CreateVersion7());

        // Assert
        assignableAgents.ShouldBeNull();
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task GetCategoriesAsync_ActiveAndInactiveCategories_ReturnsEveryCategoryByName()
    {
        // Arrange
        var software = new CategoryBuilder().WithCode("SOFTWARE").WithName("Szoftver").Build();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").WithName("Hardver").Build();
        var other = new CategoryBuilder().WithCode("OTHER").WithName("Egyeb").Build();
        other.Deactivate();
        await SaveAsync(software, hardware, other);

        // Act
        IReadOnlyList<CategoryListItemDto> categories;
        await using (var scope = Database.CreateScope())
        {
            categories = await scope.ServiceProvider.GetRequiredService<ITicketQueries>()
                .GetCategoriesAsync(CancellationToken);
        }

        // Assert
        categories.ShouldBe(
        [
            new CategoryListItemDto(other.Id, "OTHER", "Egyeb", other.Description, IsActive: false),
            new CategoryListItemDto(hardware.Id, "HARDWARE", "Hardver", hardware.Description, IsActive: true),
            new CategoryListItemDto(software.Id, "SOFTWARE", "Szoftver", software.Description, IsActive: true),
        ]);
    }

    [Fact]
    [Trait("BR", "BR-05")]
    [Trait("BR", "BR-12")]
    [Trait("BR", "BR-13")]
    public async Task GetAgentsAsync_AgentsWithCompetenciesAndTickets_ReturnsEveryAgentWithCompetenciesAndLoad()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var users = Enumerable.Range(0, 3).Select(_ => new UserBuilder().Build()).ToArray();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").WithName("Hardver").Build();
        var network = new CategoryBuilder().WithCode("NETWORK").WithName("Halozat").Build();
        var other = new CategoryBuilder().WithCode("OTHER").WithName("Egyeb").Build();
        var csaba = new AgentBuilder(users[0]).WithDisplayName("Tóth Csaba").Build();
        var anna = new AgentBuilder(users[1]).WithDisplayName("Kiss Anna").WithCompetencies(hardware, network, other).Build();
        var bela = new AgentBuilder(users[2]).WithDisplayName("Nagy Béla").WithCompetencies(hardware).Build();
        var tickets = new[]
        {
            // Open tickets of Anna: High + Critical.
            new TicketBuilder(reporter, hardware).WithImpactAndUrgency(Impact.High, Urgency.Medium)
                .InStatus(TicketStatus.Assigned, anna, users[1]).Build(),
            new TicketBuilder(reporter, network).WithImpactAndUrgency(Impact.High, Urgency.High)
                .InStatus(TicketStatus.InProgress, anna, users[1]).Build(),
            // Not open: they do not count.
            new TicketBuilder(reporter, hardware).InStatus(TicketStatus.Resolved, anna, users[1]).Build(),
            new TicketBuilder(reporter, hardware).InStatus(TicketStatus.Closed, anna, users[1]).Build(),
            // The open ticket of Béla, assigned before his deactivation: Medium.
            new TicketBuilder(reporter, hardware).InStatus(TicketStatus.Assigned, bela, users[2]).Build(),
        };
        bela.Deactivate();
        other.Deactivate();
        await SaveAsync([reporter, .. users, hardware, network, other, csaba, anna, bela, .. tickets]);

        // Act
        IReadOnlyList<AgentListItemDto> agents;
        await using (var scope = Database.CreateScope())
        {
            agents = await scope.ServiceProvider.GetRequiredService<ITicketQueries>().GetAgentsAsync(CancellationToken);
        }

        // Assert
        agents.Select(agent => (agent.Id, agent.DisplayName, agent.IsActive, agent.Load, agent.OpenTicketCount)).ShouldBe(
        [
            (anna.Id, "Kiss Anna", true, 9, 2), // 4 + 5
            (bela.Id, "Nagy Béla", false, 3, 1),
            (csaba.Id, "Tóth Csaba", true, 0, 0),
        ]);
        agents[0].Competencies.ShouldBe(
        [
            new AgentCompetencyDto(other.Id, "OTHER", "Egyeb", IsActive: false),
            new AgentCompetencyDto(network.Id, "NETWORK", "Halozat", IsActive: true),
            new AgentCompetencyDto(hardware.Id, "HARDWARE", "Hardver", IsActive: true),
        ]);
        agents[1].Competencies.ShouldBe([new AgentCompetencyDto(hardware.Id, "HARDWARE", "Hardver", IsActive: true)]);
        agents[2].Competencies.ShouldBeEmpty();
    }

    // Four tickets, one hour apart: HARDWARE New (Medium); HARDWARE Assigned to Anna (High);
    // NETWORK InProgress of Béla (Medium); NETWORK Closed of Anna (Low).
    private async Task<QueueScenario> SaveQueueScenarioAsync()
    {
        var reporter = new UserBuilder().Build();
        var annaUser = new UserBuilder().Build();
        var belaUser = new UserBuilder().Build();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var network = new CategoryBuilder().WithCode("NETWORK").Build();
        var anna = new AgentBuilder(annaUser).WithDisplayName("Kiss Anna").WithCompetencies(hardware, network).Build();
        var bela = new AgentBuilder(belaUser).WithDisplayName("Nagy Béla").WithCompetencies(network).Build();
        var hardwareNew = new TicketBuilder(reporter, hardware).CreatedAt(TestTime.Now).Build();
        var hardwareAssignedToAnna = new TicketBuilder(reporter, hardware)
            .WithImpactAndUrgency(Impact.High, Urgency.Medium)
            .CreatedAt(TestTime.Now.AddHours(1))
            .InStatus(TicketStatus.Assigned, anna, annaUser)
            .Build();
        var networkInProgressOfBela = new TicketBuilder(reporter, network)
            .CreatedAt(TestTime.Now.AddHours(2))
            .InStatus(TicketStatus.InProgress, bela, belaUser)
            .Build();
        var networkClosedOfAnna = new TicketBuilder(reporter, network)
            .WithImpactAndUrgency(Impact.Medium, Urgency.Low)
            .CreatedAt(TestTime.Now.AddHours(3))
            .InStatus(TicketStatus.Closed, anna, annaUser)
            .Build();
        await SaveAsync(
            reporter,
            annaUser,
            belaUser,
            hardware,
            network,
            anna,
            bela,
            hardwareNew,
            hardwareAssignedToAnna,
            networkInProgressOfBela,
            networkClosedOfAnna);
        return new QueueScenario(
            network,
            anna,
            bela,
            hardwareNew,
            hardwareAssignedToAnna,
            networkInProgressOfBela,
            networkClosedOfAnna);
    }

    private async Task<PagedResult<TicketQueueItemDto>> GetTicketQueueAsync(GetTicketQueueQuery query, int pageSize)
    {
        await using var scope = Database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ITicketQueries>()
            .GetTicketQueueAsync(query, pageSize, CancellationToken);
    }

    private async Task<AssignableAgentsDto?> GetAssignableAgentsAsync(Guid ticketId)
    {
        await using var scope = Database.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ITicketQueries>()
            .GetAssignableAgentsAsync(ticketId, CancellationToken);
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

    private sealed record QueueScenario(
        Category Network,
        Agent Anna,
        Agent Bela,
        Ticket HardwareNew,
        Ticket HardwareAssignedToAnna,
        Ticket NetworkInProgressOfBela,
        Ticket NetworkClosedOfAnna);
}
