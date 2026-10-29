using NSubstitute;
using ServiceDesk.Application.Agents.SetAgentCompetencies;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Application.Common.Persistence;
using ServiceDesk.Application.Common.Security;
using ServiceDesk.Domain.Agents;
using ServiceDesk.Domain.Categories;
using ServiceDesk.Domain.Common;
using Shouldly;

namespace ServiceDesk.Application.Tests.Agents.SetAgentCompetencies;

// The real validator runs; the category repository returns the known categories among the requested Ids. Anna starts
// with a HARDWARE competency.
public sealed class SetAgentCompetenciesHandlerTests
{
    private readonly IAgentRepository _agents = Substitute.For<IAgentRepository>();
    private readonly ICategoryRepository _categories = Substitute.For<ICategoryRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly RecordingLogger<SetAgentCompetenciesHandler> _logger = new();
    private readonly Category _hardware = TestData.CreateCategory("HARDWARE");
    private readonly Category _network = TestData.CreateCategory("NETWORK");
    private readonly Category _software = TestData.CreateCategory("SOFTWARE");
    private readonly Category _inactive = TestData.CreateInactiveCategory();
    private readonly Agent _anna;

    public SetAgentCompetenciesHandlerTests()
    {
        _anna = TestData.CreateAgent("Kiss Anna", _hardware);
        _agents.GetByIdAsync(_anna.Id, Arg.Any<CancellationToken>()).Returns(_anna);
        Category[] known = [_hardware, _network, _software, _inactive];
        _categories.GetByIdsAsync(Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(call => known.Where(category => call.Arg<IReadOnlyCollection<Guid>>().Contains(category.Id)).ToList());
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_AnonymousUser_ReturnsForbiddenWithoutLoading()
    {
        // Act
        var result = await CreateHandler(TestUsers.Anonymous()).HandleAsync(Command(_network), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_Reporter_ReturnsForbiddenWithoutLoading()
    {
        // Act
        var result = await CreateHandler(TestUsers.Reporter()).HandleAsync(Command(_network), CancellationToken);

        // Assert
        result.Error.ShouldBe(AuthErrors.Forbidden);
        ShouldHaveCompetencies(_hardware);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_NullCategoryIds_ReturnsValidationErrorWithoutLoading()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new SetAgentCompetenciesCommand(_anna.Id, null!), CancellationToken);

        // Assert
        result.Error.Code.ShouldBe("Validation.CategoryIds");
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_EmptyCategoryId_ReturnsValidationErrorWithoutLoading()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new SetAgentCompetenciesCommand(_anna.Id, [_network.Id, Guid.Empty]), CancellationToken);

        // Assert
        result.Error.Code.ShouldBe(AgentErrors.CompetencyCategoryInvalid.Code);
        result.Error.Message.ShouldBe(AgentErrors.CompetencyCategoryInvalid.Message);
        await ShouldNotHaveTouchedDataAsync();
    }

    [Fact]
    public async Task HandleAsync_UnknownAgent_ReturnsAgentNotFound()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new SetAgentCompetenciesCommand(Guid.CreateVersion7(), [_network.Id]), CancellationToken);

        // Assert
        result.Error.ShouldBe(AgentErrors.NotFound);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_UnknownCategory_ReturnsCategoryNotFoundAndKeepsCompetencies()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(new SetAgentCompetenciesCommand(_anna.Id, [_network.Id, Guid.CreateVersion7()]), CancellationToken);

        // Assert
        result.Error.ShouldBe(CategoryErrors.NotFound);
        ShouldHaveCompetencies(_hardware);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task HandleAsync_NewInactiveCategory_ReturnsCategoryInactiveAndKeepsCompetencies()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(_hardware, _inactive), CancellationToken);

        // Assert
        result.Error.ShouldBe(CategoryErrors.Inactive);
        ShouldHaveCompetencies(_hardware);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
        _logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task HandleAsync_KeptCompetencyOfDeactivatedCategory_KeepsItAndSaves()
    {
        // Arrange
        _hardware.Deactivate();

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(_hardware, _network), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        ShouldHaveCompetencies(_hardware, _network);
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
    }

    [Fact]
    [Trait("BR", "BR-13")]
    public async Task HandleAsync_DeactivatedCategoryLeftOut_KeepsItSavesAndLogsResultingCount()
    {
        // Arrange
        _hardware.Deactivate();

        // Act: the UI sends only the active categories.
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(_network), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        ShouldHaveCompetencies(_hardware, _network);
        await _categories.Received(1).GetByIdsAsync(
            Arg.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 2 && ids.Contains(_hardware.Id) && ids.Contains(_network.Id)),
            CancellationToken);
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
        _logger.Entries.ShouldHaveSingleItem().Properties["CompetencyCount"].ShouldBe(2);
    }

    [Fact]
    [Trait("BR", "BR-04")]
    public async Task HandleAsync_ActiveCategories_ReplacesCompetenciesSavesAndLogs()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin())
            .HandleAsync(Command(_network, _software, _network), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        ShouldHaveCompetencies(_network, _software);
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
        var entry = _logger.Entries.ShouldHaveSingleItem();
        (entry.Properties["AgentId"], entry.Properties["CompetencyCount"], entry.Properties["ActorUserId"])
            .ShouldBe((_anna.Id, 2, TestUsers.UserId));
    }

    [Fact]
    public async Task HandleAsync_EmptyList_RemovesEveryCompetency()
    {
        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        _anna.Competencies.ShouldBeEmpty();
        await _unitOfWork.Received(1).SaveChangesAsync(CancellationToken);
    }

    [Fact]
    public async Task HandleAsync_ConcurrencyConflictOnSave_ReturnsConflictWithoutLog()
    {
        // Arrange
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Failure(GeneralErrors.ConcurrencyConflict));

        // Act
        var result = await CreateHandler(TestUsers.Admin()).HandleAsync(Command(_network), CancellationToken);

        // Assert
        result.Error.ShouldBe(GeneralErrors.ConcurrencyConflict);
        _logger.Entries.ShouldBeEmpty();
    }

    private SetAgentCompetenciesCommand Command(params Category[] categories) =>
        new(_anna.Id, categories.Select(category => category.Id).ToList());

    private SetAgentCompetenciesHandler CreateHandler(ICurrentUser currentUser) => new(
        currentUser,
        new SetAgentCompetenciesValidator(),
        _agents,
        _categories,
        _unitOfWork,
        _logger);

    private void ShouldHaveCompetencies(params Category[] categories) =>
        _anna.Competencies.Select(competency => competency.CategoryId)
            .ShouldBe(categories.Select(category => category.Id), ignoreOrder: true);

    private async Task ShouldNotHaveTouchedDataAsync()
    {
        await _agents.DidNotReceiveWithAnyArgs().GetByIdAsync(default, CancellationToken);
        await _categories.DidNotReceiveWithAnyArgs().GetByIdsAsync(default!, CancellationToken);
        await _unitOfWork.DidNotReceiveWithAnyArgs().SaveChangesAsync(CancellationToken);
    }
}
