namespace PlotTwist.Application.Assistant;

// Two ways to give a model retrieval:
// Context - search before every model call with the raw question and inject the hits. Predictable,
//           but it can't filter and it also searches for "thanks!".
// Tool    - search is a function the model may call, filling in year/genre criteria itself.
//           Smarter and cheaper, but the model can skip searching and answer from memory.
public enum AssistantMode
{
    Context,
    Tool,
}
