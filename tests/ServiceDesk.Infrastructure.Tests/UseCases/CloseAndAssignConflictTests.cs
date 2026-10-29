using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets.AssignTicketManually;
using ServiceDesk.Application.Tickets.CloseTicket;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Tests.Builders;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.UseCases;

// BR-03, AGENTS.md §7: one Admin closes a Resolved ticket while another assigns it to a different agent. In any order
// one of them gets a concurrency conflict or the BR-03 error, never a closed ticket assigned afterwards. The real
// handlers, repositories and UnitOfWork run against LocalDB, each use case in a scope of its own, as in the
// UseCaseRunner.
public sealed class CloseAndAssignConflictTests(DatabaseFixture database) : IntegrationTest(database)
{
    private readonly FakeTimeProvider _time = new(TestTime.Now.AddHours(1));

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task AssignTicketManually_TicketClosedAfterLoad_ReturnsConcurrencyConflictAndTicketStaysClosed()
    {
        // Arrange
        var scenario = await SaveResolvedTicketAsync();
        Result? closeResult = null;

        // Act: the assignment has loaded the Resolved ticket; the close saves right before the assignment does.
        var assignResult = await AssignAsync(scenario, beforeSave: async () => closeResult = await CloseAsync(scenario));

        // Assert
        closeResult.ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        assignResult.Error.ShouldBe(GeneralErrors.ConcurrencyConflict);
        var ticket = await LoadTicketAsync(scenario.Ticket.Id);
        (ticket.Status, ticket.AssigneeAgentId).ShouldBe((TicketStatus.Closed, scenario.Anna.Id));
        ShouldHaveNoChangeAfterClosing(ticket);
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task CloseTicket_TicketReassignedAfterLoad_ReturnsConcurrencyConflictAndTicketStaysResolved()
    {
        // Arrange
        var scenario = await SaveResolvedTicketAsync();
        Result? assignResult = null;

        // Act: the close has loaded the Resolved ticket; the assignment saves right before the close does.
        var closeResult = await CloseAsync(scenario, beforeSave: async () => assignResult = await AssignAsync(scenario));

        // Assert
        assignResult.ShouldNotBeNull().IsSuccess.ShouldBeTrue();
        closeResult.Error.ShouldBe(GeneralErrors.ConcurrencyConflict);
        var ticket = await LoadTicketAsync(scenario.Ticket.Id);
        (ticket.Status, ticket.AssigneeAgentId).ShouldBe((TicketStatus.Resolved, scenario.Bela.Id));
        ticket.History.ShouldNotContain(entry => entry.NewValue == nameof(TicketStatus.Closed));
    }

    [Fact]
    [Trait("BR", "BR-03")]
    public async Task AssignTicketManually_TicketClosedBeforeLoad_ReturnsClosedCannotBeAssigned()
    {
        // Arrange
        var scenario = await SaveResolvedTicketAsync();
        (await CloseAsync(scenario)).IsSuccess.ShouldBeTrue();

        // Act
        var assignResult = await AssignAsync(scenario);

        // Assert
        assignResult.Error.ShouldBe(TicketErrors.ClosedCannotBeAssigned);
        var ticket = await LoadTicketAsync(scenario.Ticket.Id);
        (ticket.Status, ticket.AssigneeAgentId).ShouldBe((TicketStatus.Closed, scenario.Anna.Id));
        ShouldHaveNoChangeAfterClosing(ticket);
    }

    // BR-03: the entry that closed the ticket is its last one, so nothing was assigned or changed after it.
    private static void ShouldHaveNoChangeAfterClosing(Ticket ticket)
    {
        var last = ticket.History.Last();
        (last.Action, last.NewValue).ShouldBe((TicketHistoryAction.StatusChanged, nameof(TicketStatus.Closed)));
    }

    // A Resolved ticket of Anna; Béla is eligible for it as well.
    private async Task<Scenario> SaveResolvedTicketAsync()
    {
        var reporter = new UserBuilder().Build();
        var annaUser = new UserBuilder().Build();
        var belaUser = new UserBuilder().Build();
        var category = new CategoryBuilder().Build();
        var anna = new AgentBuilder(annaUser).WithDisplayName("Kiss Anna").WithCompetencies(category).Build();
        var bela = new AgentBuilder(belaUser).WithDisplayName("Nagy Béla").WithCompetencies(category).Build();
        var ticket = new TicketBuilder(reporter, category).InStatus(TicketStatus.Resolved, anna, annaUser).Build();
        await SaveAsync(reporter, annaUser, belaUser, category, anna, bela, ticket);
        return new Scenario(ticket, anna, bela, ClosingAdmin: annaUser, AssigningAdmin: belaUser);
    }

    private async Task<Result> CloseAsync(Scenario scenario, Func<Task>? beforeSave = null)
    {
        await using var scope = Database.CreateScope();
        var services = scope.ServiceProvider;
        var handler = new CloseTicketHandler(
            new TestCurrentUser(scenario.ClosingAdmin.Id, Roles.Admin),
            services.GetRequiredService<ITicketRepository>(),
            UnitOfWorkOf(services, beforeSave),
            _time,
            services.GetRequiredService<ILogger<CloseTicketHandler>>());
        return await handler.HandleAsync(new CloseTicketCommand(scenario.Ticket.Id), CancellationToken);
    }

    private async Task<Result> AssignAsync(Scenario scenario, Func<Task>? beforeSave = null)
    {
        await using var scope = Database.CreateScope();
        var services = scope.ServiceProvider;
        var handler = new AssignTicketManuallyHandler(
            new TestCurrentUser(scenario.AssigningAdmin.Id, Roles.Admin),
            new AssignTicketManuallyValidator(),
            services.GetRequiredService<ITicketRepository>(),
            services.GetRequiredService<IAgentRepository>(),
            UnitOfWorkOf(services, beforeSave),
            _time,
            services.GetRequiredService<ILogger<AssignTicketManuallyHandler>>());
        return await handler.HandleAsync(new AssignTicketManuallyCommand(scenario.Ticket.Id, scenario.Bela.Id), CancellationToken);
    }

    private static IUnitOfWork UnitOfWorkOf(IServiceProvider services, Func<Task>? beforeSave)
    {
        var unitOfWork = services.GetRequiredService<IUnitOfWork>();
        return beforeSave is null ? unitOfWork : new BeforeSaveUnitOfWork(unitOfWork, beforeSave);
    }

    private async Task<Ticket> LoadTicketAsync(Guid ticketId)
    {
        await using var scope = Database.CreateScope();
        var ticket = await scope.ServiceProvider.GetRequiredService<ITicketRepository>().GetByIdAsync(ticketId, CancellationToken);
        return ticket.ShouldNotBeNull();
    }

    private sealed record Scenario(
        Ticket Ticket,
        Agent Anna,
        Agent Bela,
        ApplicationUser ClosingAdmin,
        ApplicationUser AssigningAdmin);
}
