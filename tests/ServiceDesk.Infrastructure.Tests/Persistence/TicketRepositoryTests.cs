using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Infrastructure.Tests.Builders;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Persistence;

public sealed class TicketRepositoryTests(DatabaseFixture database) : IntegrationTest(database)
{
    [Fact]
    [Trait("BR", "BR-10")]
    public async Task GetByIdAsync_SavedTicket_ReturnsTicketWithHistoryInSequenceOrder()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var admin = new UserBuilder().Build();
        var category = new CategoryBuilder().Build();
        var agent = new AgentBuilder(admin).WithCompetencies(category).Build();
        var ticket = new TicketBuilder(reporter, category)
            .WithImpactAndUrgency(Impact.High, Urgency.Medium)
            .InStatus(TicketStatus.InProgress, agent, admin)
            .Build();
        await SaveAsync(reporter, admin, category, agent);
        await using (var scope = Database.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<ITicketRepository>().Add(ticket);
            var saveResult = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken);
            saveResult.IsSuccess.ShouldBeTrue();
        }

        // Act
        await using var readScope = Database.CreateScope();
        var loaded = await readScope.ServiceProvider.GetRequiredService<ITicketRepository>()
            .GetByIdAsync(ticket.Id, CancellationToken);

        // Assert
        loaded.ShouldNotBeNull();
        loaded.ReporterId.ShouldBe(reporter.Id);
        loaded.Title.ShouldBe(ticket.Title);
        loaded.Description.ShouldBe(ticket.Description);
        loaded.Impact.ShouldBe(Impact.High);
        loaded.Urgency.ShouldBe(Urgency.Medium);
        loaded.Priority.ShouldBe(Priority.High);
        loaded.CategoryId.ShouldBe(category.Id);
        loaded.Status.ShouldBe(TicketStatus.InProgress);
        loaded.AssigneeAgentId.ShouldBe(agent.Id);
        loaded.CreatedAt.ShouldBe(TestTime.Now);
        // Created, Assigned, StatusChanged (New → Assigned), StatusChanged (Assigned → InProgress).
        loaded.History.Select(HistoryValues).ShouldBe(ticket.History.Select(HistoryValues));
        loaded.History.Select(entry => entry.Sequence).ShouldBe([1, 2, 3, 4]);
    }

    [Fact]
    [Trait("BR", "BR-10")]
    public async Task SaveChangesAsync_ChangeOnLoadedTicket_AppendsHistoryEntry()
    {
        // Arrange
        var reporter = new UserBuilder().Build();
        var admin = new UserBuilder().Build();
        var category = new CategoryBuilder().Build();
        var agent = new AgentBuilder(admin).WithCompetencies(category).Build();
        var ticket = new TicketBuilder(reporter, category).InStatus(TicketStatus.InProgress, agent, admin).Build();
        await SaveAsync(reporter, admin, category, agent, ticket);
        var resolvedAt = TestTime.Now.AddHours(1);

        // Act
        await using (var scope = Database.CreateScope())
        {
            var loaded = await scope.ServiceProvider.GetRequiredService<ITicketRepository>()
                .GetByIdAsync(ticket.Id, CancellationToken);
            loaded.ShouldNotBeNull().Resolve(admin.Id, resolvedAt).IsSuccess.ShouldBeTrue();
            var saveResult = await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken);
            saveResult.IsSuccess.ShouldBeTrue();
        }

        // Assert
        await using var readScope = Database.CreateScope();
        var reloaded = await readScope.ServiceProvider.GetRequiredService<ITicketRepository>()
            .GetByIdAsync(ticket.Id, CancellationToken);
        reloaded.ShouldNotBeNull();
        reloaded.Status.ShouldBe(TicketStatus.Resolved);
        reloaded.History.Count.ShouldBe(5);
        HistoryValues(reloaded.History.Last()).ShouldBe(
            (5, admin.Id, resolvedAt, TicketHistoryAction.StatusChanged, nameof(TicketStatus.InProgress), nameof(TicketStatus.Resolved)));
    }

    private static (int Sequence, string? ActorUserId, DateTimeOffset At, TicketHistoryAction Action, string? OldValue, string? NewValue)
        HistoryValues(TicketHistoryEntry entry) =>
        (entry.Sequence, entry.ActorUserId, entry.At, entry.Action, entry.OldValue, entry.NewValue);
}
