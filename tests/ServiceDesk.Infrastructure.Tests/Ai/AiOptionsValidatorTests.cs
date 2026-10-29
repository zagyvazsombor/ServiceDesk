using ServiceDesk.Infrastructure.Ai;
using Shouldly;

namespace ServiceDesk.Infrastructure.Tests.Ai;

public sealed class AiOptionsValidatorTests
{
    private readonly AiOptionsValidator _validator = new();

    [Fact]
    public void Validate_AppSettingsValues_Succeeds()
    {
        // Act
        var result = _validator.Validate(name: null, TestAi.OllamaOptions());

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_OpenAICompatibleWithApiKey_Succeeds()
    {
        // Arrange
        var options = OpenAICompatibleOptions(apiKey: "test-key");

        // Act
        var result = _validator.Validate(name: null, options);

        // Assert
        result.Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("ZeroTimeout", "Ai:TimeoutSeconds")]
    [InlineData("UnknownPromptVersion", "Ai:PromptVersion")]
    [InlineData("EmptyEndpoint", "Ai:Ollama:Endpoint")]
    [InlineData("EndpointWithoutScheme", "Ai:Ollama:Endpoint")]
    [InlineData("BlankModel", "Ai:Ollama:Model")]
    [InlineData("TooLongModel", "Ai:Ollama:Model")]
    [InlineData("UnknownProvider", "Ai:Provider")]
    public void Validate_InvalidSetting_FailsNamingTheSetting(string invalidCase, string expectedSetting)
    {
        // Arrange
        var options = TestAi.OllamaOptions();
        switch (invalidCase)
        {
            case "ZeroTimeout":
                options.TimeoutSeconds = 0;
                break;
            case "UnknownPromptVersion":
                options.PromptVersion = "v2";
                break;
            case "EmptyEndpoint":
                options.Ollama.Endpoint = string.Empty;
                break;
            case "EndpointWithoutScheme":
                options.Ollama.Endpoint = "localhost:11434";
                break;
            case "BlankModel":
                options.Ollama.Model = " ";
                break;
            case "TooLongModel":
                options.Ollama.Model = new string('m', 101);
                break;
            case "UnknownProvider":
                options.Provider = (AiProvider)99;
                break;
        }

        // Act
        var result = _validator.Validate(name: null, options);

        // Assert
        result.Failures.ShouldNotBeNull().ShouldHaveSingleItem().ShouldStartWith(expectedSetting);
    }

    // AGENTS.md §8, §9: the key comes from User Secrets; without it the host does not start with this provider.
    [Fact]
    public void Validate_OpenAICompatibleWithoutApiKey_Fails()
    {
        // Arrange
        var options = OpenAICompatibleOptions(apiKey: null);

        // Act
        var result = _validator.Validate(name: null, options);

        // Assert
        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Ai:OpenAICompatible:ApiKey");
    }

    // Only the selected provider has to be configured; the empty Ollama section is not checked.
    [Fact]
    public void Validate_OpenAICompatibleWithoutEndpoint_FailsOnlyForThatProvider()
    {
        // Arrange
        var options = OpenAICompatibleOptions(apiKey: "test-key");
        options.OpenAICompatible.Endpoint = string.Empty;

        // Act
        var result = _validator.Validate(name: null, options);

        // Assert
        result.Failures.ShouldNotBeNull().ShouldHaveSingleItem().ShouldContain("Ai:OpenAICompatible:Endpoint");
    }

    private static AiOptions OpenAICompatibleOptions(string? apiKey) => new()
    {
        Provider = AiProvider.OpenAICompatible,
        TimeoutSeconds = 20,
        PromptVersion = "v1",
        OpenAICompatible = new OpenAICompatibleOptions { Endpoint = "https://llm.example.test/v1", Model = "test-model", ApiKey = apiKey },
    };
}
