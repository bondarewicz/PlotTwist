namespace PlotTwist.UnitTests.Fakes;

using System.Runtime.CompilerServices;
using PlotTwist.Application.Assistant;

internal sealed class FakeMovieAssistant : IMovieAssistant
{
    public string Model => "fake-model";

    public string ProviderName => "fake";

    public string? LastQuestion { get; private set; }

    public IMovieConversation StartConversation(AssistantMode mode) => new Conversation(this, mode);

    private sealed class Conversation(FakeMovieAssistant owner, AssistantMode mode) : IMovieConversation
    {
        public AssistantMode Mode { get; } = mode;

        public Task<AssistantAnswer> Ask(string question, CancellationToken ct = default)
        {
            owner.LastQuestion = question;
            return Task.FromResult(new AssistantAnswer(question, Mode, owner.Model, [], null, TimeSpan.Zero));
        }

        public async IAsyncEnumerable<string> AskStreaming(
            string question,
            Action<AssistantSearch>? onSearch = null,
            [EnumeratorCancellation] CancellationToken ct = default
        )
        {
            owner.LastQuestion = question;
            yield return question;
            await Task.CompletedTask;
        }
    }
}
