using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using ServiceDesk.Application.Agents.DeactivateAgent;
using ServiceDesk.Application.Agents.SetAgentCompetencies;
using ServiceDesk.Application.Categories.CreateCategory;
using ServiceDesk.Application.Categories.DeactivateCategory;
using ServiceDesk.Application.Categories.ListCategories;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Application.Tickets;
using ServiceDesk.Application.Tickets.AssignTicketManually;
using ServiceDesk.Application.Tickets.CreateTicket;
using ServiceDesk.Application.Tickets.GetActiveCategories;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Assignment;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using ServiceDesk.Domain.Tickets;
using ServiceDesk.Infrastructure.Identity;
using ServiceDesk.Infrastructure.Persistence;
using ServiceDesk.Infrastructure.Tests.Builders;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.UseCases;

// The master data use cases and their effect on the ticket use cases. The real handlers, repositories, queries and
// UnitOfWork run against LocalDB, each use case in a scope of its own, as in the UseCaseRunner.
public sealed class MasterDataUseCaseTests(DatabaseFixture database) : IntegrationTest(database)
{
    private readonly FakeTimeProvider _time = new(TestTime.Now.AddHours(1));
    private readonly ApplicationUser _reporter = new UserBuilder().Build();
    private readonly ApplicationUser _admin = new UserBuilder().Build();

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task DeactivateCategory_ThenCreateTicket_CategoryNotOfferedAndRefused()
    {
        // Arrange
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var network = new CategoryBuilder().WithCode("NETWORK").Build();
        await SaveAsync(_reporter, _admin, hardware, network);

        // Act
        var deactivation = await RunAsync(services => new DeactivateCategoryHandler(
                AsAdmin(),
                services.GetRequiredService<ICategoryRepository>(),
                services.GetRequiredService<IUnitOfWork>(),
                services.GetRequiredService<ILogger<DeactivateCategoryHandler>>())
            .HandleAsync(new DeactivateCategoryCommand(hardware.Id), CancellationToken));
        var activeCategories = await RunAsync(services => new GetActiveCategoriesHandler(
                AsReporter(),
                services.GetRequiredService<ITicketQueries>())
            .HandleAsync(new GetActiveCategoriesQuery(), CancellationToken));
        var creation = await CreateTicketAsync(hardware.Id);

        // Assert
        deactivation.IsSuccess.ShouldBeTrue();
        activeCategories.Value.Select(category => category.Code).ShouldBe(["NETWORK"]);
        creation.Error.ShouldBe(CategoryErrors.Inactive);
        (await CountAsync(dbContext => dbContext.Tickets)).ShouldBe(0);
        var categories = await RunAsync(services => new ListCategoriesHandler(
                AsAdmin(),
                services.GetRequiredService<ITicketQueries>())
            .HandleAsync(new ListCategoriesQuery(), CancellationToken));
        categories.Value.Select(category => (category.Code, category.IsActive))
            .ShouldBe([("HARDWARE", false), ("NETWORK", true)], ignoreOrder: true);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task SetAgentCompetencies_NewInactiveCategory_RefusedAndCompetenciesUnchanged()
    {
        // Arrange
        var annaUser = new UserBuilder().Build();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var other = new CategoryBuilder().WithCode("OTHER").Build();
        other.Deactivate();
        var anna = new AgentBuilder(annaUser).WithCompetencies(hardware).Build();
        await SaveAsync(_admin, annaUser, hardware, other, anna);

        // Act
        var result = await SetCompetenciesAsync(anna.Id, hardware.Id, other.Id);

        // Assert
        result.Error.ShouldBe(CategoryErrors.Inactive);
        (await LoadCompetenciesAsync(anna.Id)).ShouldBe([hardware.Id]);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task SetAgentCompetencies_KeptCompetencyOfDeactivatedCategory_SavesNewSet()
    {
        // Arrange
        var annaUser = new UserBuilder().Build();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var network = new CategoryBuilder().WithCode("NETWORK").Build();
        var other = new CategoryBuilder().WithCode("OTHER").Build();
        var anna = new AgentBuilder(annaUser).WithCompetencies(hardware, other).Build();
        other.Deactivate();
        await SaveAsync(_admin, annaUser, hardware, network, other, anna);

        // Act
        var result = await SetCompetenciesAsync(anna.Id, other.Id, network.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await LoadCompetenciesAsync(anna.Id)).ShouldBe([other.Id, network.Id], ignoreOrder: true);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task SetAgentCompetencies_DeactivatedCategoryLeftOut_KeepsIt()
    {
        // Arrange
        var annaUser = new UserBuilder().Build();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var network = new CategoryBuilder().WithCode("NETWORK").Build();
        var other = new CategoryBuilder().WithCode("OTHER").Build();
        var anna = new AgentBuilder(annaUser).WithCompetencies(hardware, other).Build();
        other.Deactivate();
        await SaveAsync(_admin, annaUser, hardware, network, other, anna);

        // Act: only active categories, as the UI sends them.
        var result = await SetCompetenciesAsync(anna.Id, network.Id);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        (await LoadCompetenciesAsync(anna.Id)).ShouldBe([other.Id, network.Id], ignoreOrder: true);
    }

    [Fact]
    [Trait("BR", "BR-04")]
    [Trait("BR", "BR-12")]
    public async Task DeactivateAgent_AgentWithOpenTickets_TicketsUnchangedAndNewTicketGoesToOtherAgent()
    {
        // Arrange: Anna has the smaller load (2 vs 5), so while active she would get the next HARDWARE ticket.
        var annaUser = new UserBuilder().Build();
        var belaUser = new UserBuilder().Build();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var anna = new AgentBuilder(annaUser).WithDisplayName("Kiss Anna").WithCompetencies(hardware).Build();
        var bela = new AgentBuilder(belaUser).WithDisplayName("Nagy Béla").WithCompetencies(hardware).Build();
        var annaAssigned = new TicketBuilder(_reporter, hardware).WithImpactAndUrgency(Impact.Low, Urgency.Low)
            .InStatus(TicketStatus.Assigned, anna, annaUser).Build();
        var annaInProgress = new TicketBuilder(_reporter, hardware).WithImpactAndUrgency(Impact.Low, Urgency.Low)
            .InStatus(TicketStatus.InProgress, anna, annaUser).Build();
        var belaInProgress = new TicketBuilder(_reporter, hardware).WithImpactAndUrgency(Impact.High, Urgency.High)
            .InStatus(TicketStatus.InProgress, bela, belaUser).Build();
        await SaveAsync(_reporter, _admin, annaUser, belaUser, hardware, anna, bela, annaAssigned, annaInProgress, belaInProgress);

        // Act
        var deactivation = await DeactivateAgentAsync(anna.Id);
        var creation = await CreateTicketAsync(hardware.Id);
        var manualAssignment = await AssignManuallyAsync(creation.Value, anna.Id);

        // Assert
        deactivation.IsSuccess.ShouldBeTrue();
        var reloadedAssigned = await LoadTicketAsync(annaAssigned.Id);
        (reloadedAssigned.Status, reloadedAssigned.AssigneeAgentId, reloadedAssigned.History.Count)
            .ShouldBe((TicketStatus.Assigned, anna.Id, annaAssigned.History.Count));
        var reloadedInProgress = await LoadTicketAsync(annaInProgress.Id);
        (reloadedInProgress.Status, reloadedInProgress.AssigneeAgentId, reloadedInProgress.History.Count)
            .ShouldBe((TicketStatus.InProgress, anna.Id, annaInProgress.History.Count));
        var newTicket = await LoadTicketAsync(creation.Value);
        (newTicket.Status, newTicket.AssigneeAgentId).ShouldBe((TicketStatus.Assigned, bela.Id));
        manualAssignment.Error.ShouldBe(AgentErrors.Inactive);
        (await LoadTicketAsync(creation.Value)).AssigneeAgentId.ShouldBe(bela.Id);
    }

    [Fact]
    [Trait("BR", "BR-04")]
    [Trait("BR", "BR-06")]
    [Trait("BR", "BR-12")]
    public async Task DeactivateAgent_OnlyEligibleAgent_NewTicketStaysNewWithoutAssignee()
    {
        // Arrange
        var annaUser = new UserBuilder().Build();
        var hardware = new CategoryBuilder().WithCode("HARDWARE").Build();
        var anna = new AgentBuilder(annaUser).WithCompetencies(hardware).Build();
        await SaveAsync(_reporter, _admin, annaUser, hardware, anna);

        // Act
        (await DeactivateAgentAsync(anna.Id)).IsSuccess.ShouldBeTrue();
        var creation = await CreateTicketAsync(hardware.Id);

        // Assert
        var ticket = await LoadTicketAsync(creation.Value);
        (ticket.Status, ticket.AssigneeAgentId).ShouldBe((TicketStatus.New, null));
    }

    [Fact]
    public async Task CreateCategory_DuplicateCode_RefusedWithValidationErrorAndSingleRow()
    {
        // Arrange
        await SaveAsync(_admin, new CategoryBuilder().WithCode("HARDWARE").Build());

        // Act
        var result = await CreateCategoryAsync(new CreateCategoryCommand(" HARDWARE ", "Hardver 2", null));

        // Assert
        result.Error.Code.ShouldBe("Validation.Code");
        result.Error.Message.ShouldBe(CategoryErrors.CodeAlreadyExists.Message);
        (await CountAsync(dbContext => dbContext.Categories)).ShouldBe(1);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task CreateCategory_NewCode_SavesActiveCategoryOfferedForNewTickets()
    {
        // Arrange
        await SaveAsync(_reporter, _admin);

        // Act
        var result = await CreateCategoryAsync(new CreateCategoryCommand("PRINTER", "Nyomtató", "Nyomtatók hibái."));

        // Assert
        result.IsSuccess.ShouldBeTrue();
        var activeCategories = await RunAsync(services => new GetActiveCategoriesHandler(
                AsReporter(),
                services.GetRequiredService<ITicketQueries>())
            .HandleAsync(new GetActiveCategoriesQuery(), CancellationToken));
        activeCategories.Value.ShouldHaveSingleItem()
            .ShouldBe(new ActiveCategoryDto(result.Value, "PRINTER", "Nyomtató", "Nyomtatók hibái."));
    }

    private TestCurrentUser AsAdmin() => new(_admin.Id, Roles.Admin);

    private TestCurrentUser AsReporter() => new(_reporter.Id, Roles.Reporter);

    private async Task<TResult> RunAsync<TResult>(Func<IServiceProvider, Task<TResult>> useCase)
    {
        await using var scope = Database.CreateScope();
        return await useCase(scope.ServiceProvider);
    }

    private Task<Result<Guid>> CreateCategoryAsync(CreateCategoryCommand command) =>
        RunAsync(services => new CreateCategoryHandler(
                AsAdmin(),
                new CreateCategoryValidator(services.GetRequiredService<ICategoryRepository>()),
                services.GetRequiredService<ICategoryRepository>(),
                services.GetRequiredService<IUnitOfWork>(),
                services.GetRequiredService<ILogger<CreateCategoryHandler>>())
            .HandleAsync(command, CancellationToken));

    private Task<Result> SetCompetenciesAsync(Guid agentId, params Guid[] categoryIds) =>
        RunAsync(services => new SetAgentCompetenciesHandler(
                AsAdmin(),
                new SetAgentCompetenciesValidator(),
                services.GetRequiredService<IAgentRepository>(),
                services.GetRequiredService<ICategoryRepository>(),
                services.GetRequiredService<IUnitOfWork>(),
                services.GetRequiredService<ILogger<SetAgentCompetenciesHandler>>())
            .HandleAsync(new SetAgentCompetenciesCommand(agentId, categoryIds), CancellationToken));

    private Task<Result> DeactivateAgentAsync(Guid agentId) =>
        RunAsync(services => new DeactivateAgentHandler(
                AsAdmin(),
                services.GetRequiredService<IAgentRepository>(),
                services.GetRequiredService<IUnitOfWork>(),
                services.GetRequiredService<ILogger<DeactivateAgentHandler>>())
            .HandleAsync(new DeactivateAgentCommand(agentId), CancellationToken));

    private Task<Result<Guid>> CreateTicketAsync(Guid categoryId) =>
        RunAsync(services =>
        {
            var agentRepository = services.GetRequiredService<IAgentRepository>();
            return new CreateTicketHandler(
                    AsReporter(),
                    new CreateTicketValidator(),
                    services.GetRequiredService<ICategoryRepository>(),
                    services.GetRequiredService<ICategorySuggestionRepository>(),
                    services.GetRequiredService<ITicketRepository>(),
                    new TicketAssignmentService(agentRepository, new LeastLoadedAssignmentStrategy(), _time),
                    services.GetRequiredService<IUnitOfWork>(),
                    _time,
                    services.GetRequiredService<ILogger<CreateTicketHandler>>())
                .HandleAsync(
                    new CreateTicketCommand(
                        "Nem indul a laptop",
                        "Bekapcsoláskor a laptop képernyője fekete marad.",
                        Impact.Medium,
                        Urgency.Medium,
                        categoryId),
                    CancellationToken);
        });

    private Task<Result> AssignManuallyAsync(Guid ticketId, Guid agentId) =>
        RunAsync(services => new AssignTicketManuallyHandler(
                AsAdmin(),
                new AssignTicketManuallyValidator(),
                services.GetRequiredService<ITicketRepository>(),
                services.GetRequiredService<IAgentRepository>(),
                services.GetRequiredService<IUnitOfWork>(),
                _time,
                services.GetRequiredService<ILogger<AssignTicketManuallyHandler>>())
            .HandleAsync(new AssignTicketManuallyCommand(ticketId, agentId), CancellationToken));

    private async Task<Ticket> LoadTicketAsync(Guid ticketId)
    {
        await using var scope = Database.CreateScope();
        var ticket = await scope.ServiceProvider.GetRequiredService<ITicketRepository>().GetByIdAsync(ticketId, CancellationToken);
        return ticket.ShouldNotBeNull();
    }

    private async Task<List<Guid>> LoadCompetenciesAsync(Guid agentId)
    {
        await using var scope = Database.CreateScope();
        var agent = await scope.ServiceProvider.GetRequiredService<IAgentRepository>().GetByIdAsync(agentId, CancellationToken);
        return agent.ShouldNotBeNull().Competencies.Select(competency => competency.CategoryId).ToList();
    }

    private async Task<int> CountAsync<TEntity>(Func<AppDbContext, IQueryable<TEntity>> table)
    {
        await using var scope = Database.CreateScope();
        return await table(scope.ServiceProvider.GetRequiredService<AppDbContext>()).CountAsync(CancellationToken);
    }
}
