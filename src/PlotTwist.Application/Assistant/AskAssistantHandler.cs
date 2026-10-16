namespace PlotTwist.Application.Assistant;

using PlotTwist.Domain;

// Single question, fresh conversation: the stateless shape the Api needs. The Cli chat keeps a conversation open instead.
public sealed class AskAssistantHandler(IMovieAssistant assistant)
{
    public Task<AssistantAnswer> Handle(AskAssistant command, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(command.Question))
            throw new DomainException("Ask a question.");
        if (command.Question.Length > AskAssistant.MaxQuestionLength)
            throw new DomainException($"The question is longer than {AskAssistant.MaxQuestionLength} characters.");

        return assistant.StartConversation(command.Mode).Ask(command.Question.Trim(), ct);
    }
}
