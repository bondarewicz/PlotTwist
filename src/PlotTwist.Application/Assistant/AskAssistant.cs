namespace PlotTwist.Application.Assistant;

public sealed record AskAssistant(string Question, AssistantMode Mode = AssistantMode.Tool)
{
    public const int MaxQuestionLength = 1000;
}
