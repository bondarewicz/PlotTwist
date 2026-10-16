namespace PlotTwist.UnitTests.Application;

using Fakes;
using PlotTwist.Application.Assistant;
using PlotTwist.Domain;

public sealed class AskAssistantHandlerTests
{
    private readonly FakeMovieAssistant assistant = new();

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Rejects_an_empty_question(string question) =>
        await Assert.ThrowsAsync<DomainException>(() =>
            new AskAssistantHandler(assistant).Handle(new AskAssistant(question), TestContext.Current.CancellationToken)
        );

    [Fact]
    public async Task Rejects_a_question_over_the_length_limit()
    {
        var question = new string('a', AskAssistant.MaxQuestionLength + 1);

        await Assert.ThrowsAsync<DomainException>(() =>
            new AskAssistantHandler(assistant).Handle(new AskAssistant(question), TestContext.Current.CancellationToken)
        );
    }

    [Fact]
    public async Task Trims_the_question_before_asking_the_assistant()
    {
        await new AskAssistantHandler(assistant).Handle(
            new AskAssistant("  what is Alien about?  "),
            TestContext.Current.CancellationToken
        );

        Assert.Equal("what is Alien about?", assistant.LastQuestion);
    }
}
