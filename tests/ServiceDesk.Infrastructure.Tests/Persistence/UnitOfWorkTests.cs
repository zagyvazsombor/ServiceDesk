using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Tests.Builders;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Persistence;

public sealed class UnitOfWorkTests(DatabaseFixture database) : IntegrationTest(database)
{
    // SQL Server: duplicate key row in a unique index.
    private const int UniqueIndexViolation = 2601;

    [Fact]
    public async Task SaveChangesAsync_SameTicketChangedInTwoContexts_ReturnsConcurrencyConflict()
    {
        // Arrange
        var (ticket, admin) = await SaveAssignedTicketAsync();
        var changedAt = TestTime.Now.AddHours(1);
        await using var firstScope = Database.CreateScope();
        await using var secondScope = Database.CreateScope();
        var firstTicket = await LoadTicketAsync(firstScope, ticket.Id);
        var secondTicket = await LoadTicketAsync(secondScope, ticket.Id);
        firstTicket.Start(admin.Id, changedAt).IsSuccess.ShouldBeTrue();
        secondTicket.ChangeImpactAndUrgency(Impact.High, Urgency.High, admin.Id, changedAt).IsSuccess.ShouldBeTrue();
        (await SaveChangesAsync(firstScope)).IsSuccess.ShouldBeTrue();

        // Act
        var result = await SaveChangesAsync(secondScope);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GeneralErrors.ConcurrencyConflict);
        await using var readScope = Database.CreateScope();
        var reloaded = await LoadTicketAsync(readScope, ticket.Id);
        reloaded.Status.ShouldBe(TicketStatus.InProgress);
        reloaded.Impact.ShouldBe(Impact.Medium);
        reloaded.Urgency.ShouldBe(Urgency.Medium);
        reloaded.History.Count.ShouldBe(4);
    }

    [Fact]
    public async Task SaveChangesAsync_StaleRowVersion_ReturnsConcurrencyConflict()
    {
        // Arrange
        var (ticket, admin) = await SaveAssignedTicketAsync();
        await using var firstScope = Database.CreateScope();
        await using var secondScope = Database.CreateScope();
        var firstTicket = await LoadTicketAsync(firstScope, ticket.Id);
        var secondTicket = await LoadTicketAsync(secondScope, ticket.Id);
        firstTicket.Start(admin.Id, TestTime.Now.AddHours(1)).IsSuccess.ShouldBeTrue();
        (await SaveChangesAsync(firstScope)).IsSuccess.ShouldBeTrue();
        // Every domain method appends a history entry, whose INSERT fails first on the (TicketId, Sequence) index.
        // A change without history reaches the RowVersion check only, so it is made through the change tracker.
        secondScope.ServiceProvider.GetRequiredService<AppDbContext>()
            .Entry(secondTicket)
            .Property(changed => changed.Title)
            .CurrentValue = "Módosított cím a második munkamenetben";

        // Act
        var result = await SaveChangesAsync(secondScope);

        // Assert
        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(GeneralErrors.ConcurrencyConflict);
        await using var readScope = Database.CreateScope();
        var reloaded = await LoadTicketAsync(readScope, ticket.Id);
        reloaded.Title.ShouldBe(ticket.Title);
        reloaded.Status.ShouldBe(TicketStatus.InProgress);
    }

    [Fact]
    public async Task SaveChangesAsync_DuplicateCategoryCode_ReturnsCodeAlreadyExists()
    {
        // Arrange: the validator has not seen the other category, as with two categories created at the same time.
        await SaveAsync(new CategoryBuilder().WithCode("DUPLICATE").Build());
        await using var scope = Database.CreateScope();
        scope.ServiceProvider.GetRequiredService<ICategoryRepository>()
            .Add(new CategoryBuilder().WithCode("DUPLICATE").Build());

        // Act
        var result = await SaveChangesAsync(scope);

        // Assert
        result.Error.ShouldBe(CategoryErrors.CodeAlreadyExists);
        await using var readScope = Database.CreateScope();
        var codes = await readScope.ServiceProvider.GetRequiredService<AppDbContext>().Categories
            .Select(category => category.Code)
            .ToListAsync(CancellationToken);
        codes.ShouldBe(["DUPLICATE"]);
    }

    [Fact]
    public async Task SaveChangesAsync_DuplicateAgentUser_ThrowsDbUpdateException()
    {
        // Arrange
        var user = new UserBuilder().Build();
        await SaveAsync(user, new AgentBuilder(user).Build());
        await using var scope = Database.CreateScope();
        scope.ServiceProvider.GetRequiredService<AppDbContext>().Agents.Add(new AgentBuilder(user).Build());

        // Act
        var exception = await Should.ThrowAsync<DbUpdateException>(() => SaveChangesAsync(scope));

        // Assert: only a concurrent history append and a duplicate category code are expected; any other unique index
        // violation stays an unexpected error (AGENTS.md §11).
        exception.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(UniqueIndexViolation);
    }

    private async Task<(Ticket Ticket, ApplicationUser Admin)> SaveAssignedTicketAsync()
    {
        var reporter = new UserBuilder().Build();
        var admin = new UserBuilder().Build();
        var category = new CategoryBuilder().Build();
        var agent = new AgentBuilder(admin).WithCompetencies(category).Build();
        var ticket = new TicketBuilder(reporter, category).InStatus(TicketStatus.Assigned, agent, admin).Build();
        await SaveAsync(reporter, admin, category, agent, ticket);
        return (ticket, admin);
    }

    private static async Task<Ticket> LoadTicketAsync(AsyncServiceScope scope, Guid ticketId)
    {
        var ticket = await scope.ServiceProvider.GetRequiredService<ITicketRepository>()
            .GetByIdAsync(ticketId, CancellationToken);
        return ticket.ShouldNotBeNull();
    }

    private static Task<Result> SaveChangesAsync(AsyncServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(CancellationToken);
}
