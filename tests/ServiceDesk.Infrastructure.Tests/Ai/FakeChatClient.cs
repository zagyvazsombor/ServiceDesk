using Microsoft.Extensions.AI;

namespace ServiceDesk.Infrastructure.Tests.Ai;

// Stands in for the model: records the request and answers with the text of the respond callback, which can also
// throw, move a FakeTimeProvider or wait for the cancellation.
internal sealed class FakeChatClient(Func<CancellationToken, Task<string>> respond) : IChatClient
{
    public List<ChatMessage> Messages { get; } = [];

    public ChatOptions? Options { get; private set; }

    public static FakeChatClient Answering(string text) => new(_ => Task.FromResult(text));

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Messages.AddRange(messages);
        Options = options;

        var text = await respond(cancellationToken);
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
    }

    public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("The suggester does not stream.");

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
    }
}
