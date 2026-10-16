namespace PlotTwist.Cli.Commands;

using System.CommandLine;
using Application.Assistant;
using Microsoft.Extensions.DependencyInjection;

internal static class ChatCommand
{
    public static Command Create(IServiceProvider services)
    {
        var mode = new Option<AssistantMode>("--mode")
        {
            Description =
                "Context: search before every turn. Tool: the model decides when to search and with which filters.",
            DefaultValueFactory = _ => AssistantMode.Tool,
        };

        var command = new Command(
            "chat",
            "Talk to the Agent Framework assistant; grey lines show the searches it runs."
        )
        {
            mode,
        };
        command.SetAction(
            async (parse, ct) =>
            {
                var assistant = services.GetRequiredService<IMovieAssistant>();
                var conversation = assistant.StartConversation(parse.GetValue(mode));
                Console.WriteLine(
                    $"PlotTwist ({conversation.Mode} mode, {assistant.Model}). Describe a film; empty line to quit."
                );

                while (!ct.IsCancellationRequested)
                {
                    Console.Write("\n> ");
                    var question = Console.ReadLine();
                    if (string.IsNullOrWhiteSpace(question))
                        break;

                    await foreach (var text in conversation.AskStreaming(question, PrintSearch, ct))
                        Console.Write(text);
                    Console.WriteLine();
                }
            }
        );
        return command;
    }

    private static void PrintSearch(AssistantSearch search)
    {
        Console.ForegroundColor = ConsoleColor.DarkGray;
        Console.WriteLine(
            $"[{search.RequestedBy} searched] \"{search.Result.Description}\" where {search.Result.Criteria} -> {search.Result.Matches.Count} films"
        );
        Console.ResetColor();
    }
}
