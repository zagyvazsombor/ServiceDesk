using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ServiceDesk.Application.Common.Ai;
using ServiceDesk.Application.Common.Errors;
using ServiceDesk.Infrastructure.Ai;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Ai;

// AGENTS.md §9: AddInfrastructure chooses the chat client from the Ai section and validates it on start. Only the
// unreachable Ollama test sends a request, to a local port where nothing listens.
public sealed class AiServiceRegistrationTests
{
    [Fact]
    public void AddInfrastructure_OllamaProvider_RegistersOllamaChatClient()
    {
        // Arrange
        using var services = BuildServices(new Dictionary<string, string?>
        {
            ["Ai:Provider"] = "Ollama",
            ["Ai:Ollama:Endpoint"] = "http://localhost:11434",
            ["Ai:Ollama:Model"] = "gemma3:4b",
        });

        // Act
        var metadata = services.GetRequiredService<IChatClient>().GetService<ChatClientMetadata>();

        // Assert
        metadata.ShouldNotBeNull();
        metadata.ProviderName.ShouldBe("ollama");
        metadata.ProviderUri.ShouldBe(new Uri("http://localhost:11434"));
        metadata.DefaultModelId.ShouldBe("gemma3:4b");
    }

    [Fact]
    public void AddInfrastructure_OpenAICompatibleProvider_RegistersOpenAIChatClient()
    {
        // Arrange
        using var services = BuildServices(new Dictionary<string, string?>
        {
            ["Ai:Provider"] = "OpenAICompatible",
            ["Ai:OpenAICompatible:Endpoint"] = "https://llm.example.test/v1",
            ["Ai:OpenAICompatible:Model"] = "test-model",
            ["Ai:OpenAICompatible:ApiKey"] = "test-key",
        });

        // Act
        var metadata = services.GetRequiredService<IChatClient>().GetService<ChatClientMetadata>();

        // Assert
        metadata.ShouldNotBeNull();
        metadata.ProviderName.ShouldBe("openai");
        metadata.ProviderUri.ShouldBe(new Uri("https://llm.example.test/v1"));
        metadata.DefaultModelId.ShouldBe("test-model");
    }

    [Fact]
    public void AddInfrastructure_SuggestionServices_AreSingletons()
    {
        // Arrange
        using var services = BuildServices(new Dictionary<string, string?>());

        // Act
        var suggester = services.GetRequiredService<ICategorySuggester>();
        var rateLimiter = services.GetRequiredService<ISuggestionRateLimiter>();

        // Assert
        suggester.ShouldBeOfType<LlmCategorySuggester>().ShouldBeSameAs(services.GetRequiredService<ICategorySuggester>());
        rateLimiter.ShouldBeOfType<SuggestionRateLimiter>().ShouldBeSameAs(services.GetRequiredService<ISuggestionRateLimiter>());
    }

    // The acceptance criterion of P-8A with the real Ollama client: nothing listens on the port (as when Ollama is not
    // running), and the suggestion is NoSuggestion instead of an exception.
    [Fact]
    [Trait("BR", "BR-08")]
    public async Task AddInfrastructure_OllamaNotRunning_SuggestionReturnsNoSuggestion()
    {
        // Arrange
        using var services = BuildServices(new Dictionary<string, string?>
        {
            ["Ai:TimeoutSeconds"] = "5",
            ["Ai:Ollama:Endpoint"] = "http://127.0.0.1:9",
        });
        var suggester = services.GetRequiredService<ICategorySuggester>();

        // Act
        var result = await suggester.SuggestAsync(TestAi.Input(), TestContext.Current.CancellationToken);

        // Assert
        result.Error.ShouldBe(AiErrors.NoSuggestion);
    }

    // The host runs the IStartupValidator before it starts; a missing API key stops it there.
    [Fact]
    public void AddInfrastructure_InvalidAiSection_FailsStartupValidation()
    {
        // Arrange
        using var services = BuildServices(new Dictionary<string, string?>
        {
            ["Ai:Provider"] = "OpenAICompatible",
            ["Ai:OpenAICompatible:Endpoint"] = "https://llm.example.test/v1",
            ["Ai:OpenAICompatible:Model"] = "test-model",
        });

        // Act
        var act = () => services.GetRequiredService<IStartupValidator>().Validate();

        // Assert
        Should.Throw<OptionsValidationException>(act).Message.ShouldContain("Ai:OpenAICompatible:ApiKey");
    }

    [Fact]
    public void AddInfrastructure_ValidAiSection_PassesStartupValidation()
    {
        // Arrange
        using var services = BuildServices(new Dictionary<string, string?>());

        // Act
        var act = () => services.GetRequiredService<IStartupValidator>().Validate();

        // Assert
        Should.NotThrow(act);
    }

    // The values of appsettings.json, overridden by the settings of the test.
    private static ServiceProvider BuildServices(Dictionary<string, string?> settings)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = TestDatabase.ConnectionString,
                ["Ai:Provider"] = "Ollama",
                ["Ai:TimeoutSeconds"] = "20",
                ["Ai:PromptVersion"] = "v1",
                ["Ai:Ollama:Endpoint"] = "http://localhost:11434",
                ["Ai:Ollama:Model"] = "gemma3:4b",
            })
            .AddInMemoryCollection(settings)
            .Build();

        return new ServiceCollection()
            .AddLogging()
            .AddInfrastructure(configuration)
            .BuildServiceProvider();
    }
}
