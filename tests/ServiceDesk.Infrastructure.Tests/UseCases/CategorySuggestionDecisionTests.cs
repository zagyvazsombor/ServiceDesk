using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets;
using ServiceDesk.Application.Tickets.CreateTicket;
using ServiceDesk.Application.Tickets.GetTicketDetails;
using ServiceDesk.Domain.Assignment;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.CategorySuggestions;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Tests.Builders;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.UseCases;

// BR-09: the decision about the AI suggestion is saved with the new ticket in one SaveChanges, so the update of the
// suggestion has to follow the insert of the ticket it refers to. The real handlers, repositories, queries and
// UnitOfWork run against LocalDB, each use case in a scope of its own, as in the UseCaseRunner.
public sealed class CategorySuggestionDecisionTests(DatabaseFixture database) : IntegrationTest(database)
{
    private readonly FakeTimeProvider _time = new(TestTime.Now.AddHours(1));
    private readonly ApplicationUser _reporter = new UserBuilder().Build();
    private readonly ApplicationUser _admin = new UserBuilder().Build();
    private readonly Category _hardware = new CategoryBuilder().WithCode("HARDWARE").WithName("Hardver").Build();
    private readonly Category _network = new CategoryBuilder().WithCode("NETWORK").WithName("Hálózat").Build();

    [Theory]
    [Trait("BR", "BR-09")]
    [InlineData("NETWORK", CategorySuggestionDecision.Accepted)]
    [InlineData("HARDWARE", CategorySuggestionDecision.Overridden)]
    public async Task CreateTicket_PendingSuggestion_SavesTicketAndDecisionTogether(
        string chosenCode, CategorySuggestionDecision expected)
    {
        // Arrange: the model suggested NETWORK.
        var suggestion = CategorySuggestion.Create(
            _reporter.Id, _network, 0.82, "A wifi hálózati hiba.", "gemma3:4b", "v1", TestTime.Now).Value;
        await SaveAsync(_reporter, _admin, _hardware, _network, suggestion);
        var chosen = chosenCode == _network.Code ? _network : _hardware;

        // Act
        var creation = await CreateTicketAsync(chosen.Id, suggestion.Id);

        // Assert
        creation.IsSuccess.ShouldBeTrue();
        var saved = await LoadSuggestionAsync(suggestion.Id);
        saved.Decision.ShouldBe(expected);
        saved.TicketId.ShouldBe(creation.Value);
        saved.DecidedAt.ShouldBe(_time.GetUtcNow());
        (await LoadTicketAsync(creation.Value)).CategoryId.ShouldBe(chosen.Id);
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task CreateTicket_AcceptedSuggestion_AdminDetailsShowItAndReporterDetailsDoNot()
    {
        // Arrange
        var suggestion = CategorySuggestion.Create(
            _reporter.Id, _network, 0.82, "A wifi hálózati hiba.", "gemma3:4b", "v1", TestTime.Now).Value;
        await SaveAsync(_reporter, _admin, _hardware, _network, suggestion);
        var creation = await CreateTicketAsync(_network.Id, suggestion.Id);

        // Act
        var adminDetails = await GetTicketDetailsAsync(new TestCurrentUser(_admin.Id, Roles.Admin), creation.Value);
        var reporterDetails = await GetTicketDetailsAsync(new TestCurrentUser(_reporter.Id, Roles.Reporter), creation.Value);

        // Assert
        var shown = adminDetails.Value.CategorySuggestion.ShouldNotBeNull();
        shown.SuggestedCategoryName.ShouldBe("Hálózat");
        shown.Confidence.ShouldBe(0.82);
        shown.Decision.ShouldBe(CategorySuggestionDecision.Accepted);
        reporterDetails.Value.CategorySuggestion.ShouldBeNull();
    }

    [Fact]
    [Trait("BR", "BR-09")]
    public async Task CreateTicket_SuggestionOfOtherReporter_RefusedAndNothingSaved()
    {
        // Arrange
        var otherReporter = new UserBuilder().Build();
        var suggestion = CategorySuggestion.Create(
            otherReporter.Id, _network, 0.82, "A wifi hálózati hiba.", "gemma3:4b", "v1", TestTime.Now).Value;
        await SaveAsync(_reporter, otherReporter, _admin, _hardware, _network, suggestion);

        // Act
        var creation = await CreateTicketAsync(_network.Id, suggestion.Id);

        // Assert
        creation.Error.ShouldBe(CategorySuggestionErrors.NotFound);
        (await CountAsync(dbContext => dbContext.Tickets)).ShouldBe(0);
        (await LoadSuggestionAsync(suggestion.Id)).Decision.ShouldBe(CategorySuggestionDecision.Pending);
    }

    private async Task<TResult> RunAsync<TResult>(Func<IServiceProvider, Task<TResult>> useCase)
    {
        await using var scope = Database.CreateScope();
        return await useCase(scope.ServiceProvider);
    }

    private Task<Result<Guid>> CreateTicketAsync(Guid categoryId, Guid suggestionId) =>
        RunAsync(services => new CreateTicketHandler(
                new TestCurrentUser(_reporter.Id, Roles.Reporter),
                new CreateTicketValidator(),
                services.GetRequiredService<ICategoryRepository>(),
                services.GetRequiredService<ICategorySuggestionRepository>(),
                services.GetRequiredService<ITicketRepository>(),
                new TicketAssignmentService(
                    services.GetRequiredService<IAgentRepository>(), new LeastLoadedAssignmentStrategy(), _time),
                services.GetRequiredService<IUnitOfWork>(),
                _time,
                services.GetRequiredService<ILogger<CreateTicketHandler>>())
            .HandleAsync(
                new CreateTicketCommand(
                    "Nem működik a wifi a 3. emeleten",
                    "Reggel óta egyik laptop sem csatlakozik a vezeték nélküli hálózathoz.",
                    Impact.Medium,
                    Urgency.High,
                    categoryId,
                    suggestionId),
                CancellationToken));

    private Task<Result<TicketDetailsDto>> GetTicketDetailsAsync(ICurrentUser currentUser, Guid ticketId) =>
        RunAsync(services => new GetTicketDetailsHandler(
                currentUser,
                services.GetRequiredService<ITicketQueries>(),
                services.GetRequiredService<ILogger<GetTicketDetailsHandler>>())
            .HandleAsync(new GetTicketDetailsQuery(ticketId), CancellationToken));

    private async Task<CategorySuggestion> LoadSuggestionAsync(Guid suggestionId)
    {
        await using var scope = Database.CreateScope();
        var suggestion = await scope.ServiceProvider.GetRequiredService<ICategorySuggestionRepository>()
            .GetByIdAsync(suggestionId, CancellationToken);
        return suggestion.ShouldNotBeNull();
    }

    private async Task<Ticket> LoadTicketAsync(Guid ticketId)
    {
        await using var scope = Database.CreateScope();
        var ticket = await scope.ServiceProvider.GetRequiredService<ITicketRepository>().GetByIdAsync(ticketId, CancellationToken);
        return ticket.ShouldNotBeNull();
    }

    private async Task<int> CountAsync<TEntity>(Func<AppDbContext, IQueryable<TEntity>> table)
    {
        await using var scope = Database.CreateScope();
        return await table(scope.ServiceProvider.GetRequiredService<AppDbContext>()).CountAsync(CancellationToken);
    }
}
