using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Infrastructure.Ai;
using ServiceDesk.Infrastructure.Tests.Builders;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Ai;

// The adapter with a fake IChatClient; the timeout runs on a FakeTimeProvider (AGENTS.md §9, BR-08).
public sealed class LlmCategorySuggesterTests
{
    private const string Reasoning = "A leírás vezeték nélküli hálózati hibát említ.";

    private readonly AiOptions _options = TestAi.OllamaOptions();
    private readonly FakeTimeProvider _time = new(TestTime.Now);
    private readonly RecordingLogger<LlmCategorySuggester> _logger = new();

    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task SuggestAsync_ValidAnswer_ReturnsSuggestionWithModelAndPromptVersion()
    {
        // Arrange
        var chatClient = FakeChatClient.Answering(TestAi.Answer("NETWORK", 0.8, Reasoning));

        // Act
        var result = await CreateSuggester(chatClient).SuggestAsync(TestAi.Input(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
        result.Value.CategoryCode.ShouldBe("NETWORK");
        result.Value.Confidence.ShouldBe(0.8);
        result.Value.Reasoning.ShouldBe(Reasoning);
        result.Value.Model.ShouldBe(TestAi.Model);
        result.Value.PromptVersion.ShouldBe("v1");
        _logger.Entries.ShouldBeEmpty();
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task SuggestAsync_CodeInOtherCaseWithSpaces_ReturnsCodeOfTheCategory()
    {
        // Arrange
        var chatClient = FakeChatClient.Answering(TestAi.Answer("  network ", 0.8, Reasoning));

        // Act
        var result = await CreateSuggester(chatClient).SuggestAsync(TestAi.Input(), CancellationToken);

        // Assert
        result.Value.CategoryCode.ShouldBe("NETWORK");
    }

    // OTHER exists in the seed but is inactive here, so it is not among the input categories (BR-13).
    [Theory]
    [Trait("BR", "BR-08")]
    [InlineData("PRINTER")]
    [InlineData("OTHER")]
    [InlineData("")]
    public async Task SuggestAsync_CodeNotAmongActiveCategories_ReturnsNoSuggestionWithWarning(string categoryCode)
    {
        // Arrange
        var chatClient = FakeChatClient.Answering(TestAi.Answer(categoryCode, 0.9, Reasoning));

        // Act
        var result = await CreateSuggester(chatClient).SuggestAsync(TestAi.Input(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AiErrors.NoSuggestion);
        _logger.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Warning);
    }

    [Theory]
    [Trait("BR", "BR-08")]
    [InlineData("A kategória a NETWORK.")]
    [InlineData("""{"categoryCode": "NETWORK", "confidence": """)]
    [InlineData("""["NETWORK", 0.8]""")]
    [InlineData("""{"categoryCode": "NETWORK", "confidence": "magas", "reasoning": "x"}""")]
    [InlineData("null")]
    [InlineData("")]
    public async Task SuggestAsync_InvalidJson_ReturnsNoSuggestionWithWarning(string answer)
    {
        // Arrange
        var chatClient = FakeChatClient.Answering(answer);

        // Act
        var result = await CreateSuggester(chatClient).SuggestAsync(TestAi.Input(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AiErrors.NoSuggestion);
        _logger.Entries.ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Warning);
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task SuggestAsync_ModelSlowerThanTimeout_ReturnsNoSuggestionWithWarning()
    {
        // Arrange
        var chatClient = new FakeChatClient(async ct =>
        {
            _time.Advance(TimeSpan.FromSeconds(_options.TimeoutSeconds));
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return TestAi.Answer("NETWORK", 0.8, Reasoning);
        });

        // Act
        var result = await CreateSuggester(chatClient).SuggestAsync(TestAi.Input(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AiErrors.NoSuggestion);
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Properties["TimeoutSeconds"].ShouldBe(_options.TimeoutSeconds);
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task SuggestAsync_AnswerJustBeforeTimeout_ReturnsSuggestion()
    {
        // Arrange
        var chatClient = new FakeChatClient(_ =>
        {
            _time.Advance(TimeSpan.FromSeconds(_options.TimeoutSeconds) - TimeSpan.FromMilliseconds(1));
            return Task.FromResult(TestAi.Answer("NETWORK", 0.8, Reasoning));
        });

        // Act
        var result = await CreateSuggester(chatClient).SuggestAsync(TestAi.Input(), CancellationToken);

        // Assert
        result.IsSuccess.ShouldBeTrue();
    }

    // The acceptance criterion of P-8A: without a running Ollama the suggestion fails gracefully.
    [Fact]
    [Trait("BR", "BR-08")]
    public async Task SuggestAsync_ModelUnreachable_ReturnsNoSuggestionWithWarning()
    {
        // Arrange
        var unreachable = new HttpRequestException("No connection could be made because the target machine actively refused it.");
        var chatClient = new FakeChatClient(_ => throw unreachable);

        // Act
        var result = await CreateSuggester(chatClient).SuggestAsync(TestAi.Input(), CancellationToken);

        // Assert
        result.Error.ShouldBe(AiErrors.NoSuggestion);
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Level.ShouldBe(LogLevel.Warning);
        entry.Exception.ShouldBeSameAs(unreachable);
        entry.Properties["Model"].ShouldBe(TestAi.Model);
    }

    [Fact]
    public async Task SuggestAsync_CallerCancels_ThrowsOperationCanceledWithoutLog()
    {
        // Arrange
        using var caller = new CancellationTokenSource();
        var chatClient = new FakeChatClient(async ct =>
        {
            await caller.CancelAsync();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            return TestAi.Answer("NETWORK", 0.8, Reasoning);
        });

        // Act
        var act = () => CreateSuggester(chatClient).SuggestAsync(TestAi.Input(), caller.Token);

        // Assert
        await Should.ThrowAsync<OperationCanceledException>(act);
        _logger.Entries.ShouldBeEmpty();
    }

    [Theory]
    [Trait("BR", "BR-08")]
    [InlineData(-0.4, 0)]
    [InlineData(0, 0)]
    [InlineData(0.65, 0.65)]
    [InlineData(1, 1)]
    [InlineData(1.7, 1)]
    [InlineData(85, 1)]
    public async Task SuggestAsync_Confidence_IsClampedBetweenZeroAndOne(double answered, double expected)
    {
        // Arrange
        var chatClient = FakeChatClient.Answering(TestAi.Answer("NETWORK", answered, Reasoning));

        // Act
        var result = await CreateSuggester(chatClient).SuggestAsync(TestAi.Input(), CancellationToken);

        // Assert
        result.Value.Confidence.ShouldBe(expected);
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task SuggestAsync_TooLongReasoning_IsCutTo500Characters()
    {
        // Arrange
        var chatClient = FakeChatClient.Answering(TestAi.Answer("NETWORK", 0.8, new string('a', 600)));

        // Act
        var result = await CreateSuggester(chatClient).SuggestAsync(TestAi.Input(), CancellationToken);

        // Assert
        result.Value.Reasoning.ShouldBe(new string('a', 500));
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task SuggestAsync_ReasoningCutInsideSurrogatePair_DropsTheHalfCharacter()
    {
        // Arrange: the emoji takes the characters at index 499 and 500.
        var chatClient = FakeChatClient.Answering(TestAi.Answer("NETWORK", 0.8, new string('a', 499) + "😀 vége"));

        // Act
        var result = await CreateSuggester(chatClient).SuggestAsync(TestAi.Input(), CancellationToken);

        // Assert
        result.Value.Reasoning.ShouldBe(new string('a', 499));
    }

    [Fact]
    [Trait("BR", "BR-08")]
    public async Task SuggestAsync_ReasoningWithControlCharacters_ReturnsSingleLinePlainText()
    {
        // Arrange
        var chatClient = FakeChatClient.Answering(TestAi.Answer("NETWORK", 0.8, "\n Wi-Fi hiba.\r\nA hálózat\tnem elérhető.\u0007 "));

        // Act
        var result = await CreateSuggester(chatClient).SuggestAsync(TestAi.Input(), CancellationToken);

        // Assert
        result.Value.Reasoning.ShouldBe("Wi-Fi hiba.  A hálózat nem elérhető.");
    }

    // AGENTS.md §8, §11: not even an answer that repeats the ticket gets into the log.
    [Fact]
    public async Task SuggestAsync_InvalidAnswerRepeatingTheTicket_LogsWarningWithoutTicketText()
    {
        // Arrange
        var chatClient = FakeChatClient.Answering(TestAi.Answer(TestAi.Title, 0.9, TestAi.Description));

        // Act
        await CreateSuggester(chatClient).SuggestAsync(TestAi.Input(), CancellationToken);

        // Assert
        var entry = _logger.Entries.ShouldHaveSingleItem();
        entry.Message.ShouldNotContain(TestAi.Title);
        entry.Message.ShouldNotContain(TestAi.Description);
        entry.Properties.Values.OfType<string>().ShouldAllBe(value => !value.Contains(TestAi.Title) && !value.Contains(TestAi.Description));
    }

    // AGENTS.md §9: structured output with a JSON schema, and no tool or function calling.
    [Fact]
    public async Task SuggestAsync_Request_AsksForJsonSchemaWithoutTools()
    {
        // Arrange
        var chatClient = FakeChatClient.Answering(TestAi.Answer("NETWORK", 0.8, Reasoning));

        // Act
        await CreateSuggester(chatClient).SuggestAsync(TestAi.Input(), CancellationToken);

        // Assert
        var options = chatClient.Options.ShouldNotBeNull();
        var format = options.ResponseFormat.ShouldBeOfType<ChatResponseFormatJson>();
        var schema = format.Schema.ShouldNotBeNull().GetRawText();
        schema.ShouldContain("categoryCode");
        schema.ShouldContain("confidence");
        schema.ShouldContain("reasoning");
        (options.Tools ?? []).ShouldBeEmpty();
        options.ToolMode.ShouldBeNull();
        options.Temperature.ShouldBe(0);
    }

    private LlmCategorySuggester CreateSuggester(FakeChatClient chatClient) =>
        new(chatClient, Options.Create(_options), _time, _logger);
}
