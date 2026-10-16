namespace PlotTwist.Evaluation;

using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlotTwist.Application;
using PlotTwist.Infrastructure;
using PlotTwist.Infrastructure.Ai;
using PlotTwist.Infrastructure.Configuration;

// One host for the whole run: the real Application + Infrastructure wiring from appsettings.json, so the
// evaluations exercise exactly what the Api serves. Needs Postgres (docker compose) and the configured models.
public sealed class EvaluationHost : IDisposable
{
    // All scenarios of one `dotnet test` run share an execution name, so the report groups them and can
    // compare this run against earlier ones.
    private static readonly string ExecutionName = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss");

    private readonly IHost host;

    public EvaluationHost()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration.AddUserSecrets<EvaluationHost>(optional: true);
        builder.Services.AddApplication().AddInfrastructure(builder.Configuration);
        host = builder.Build();

        var judge = builder.Configuration.GetSection("Evaluation:Judge").Get<ChatModelOptions>()
            ?? throw new InvalidOperationException("Evaluation:Judge is not configured.");
        // The Quality evaluators cap the judge at 800 output tokens: enough for GPT-4o-class judges, which answer
        // tersely. Local models write their step-by-step rationale out in full and get cut off before the score,
        // so the cap is raised per request with standard chat-client middleware (Evaluation:JudgeMaxOutputTokens).
        var maxOutputTokens = builder.Configuration.GetValue<int?>("Evaluation:JudgeMaxOutputTokens");
        var judgeClient = AiClientFactory.CreateChatClient(judge)
            .AsBuilder()
            .ConfigureOptions(options => options.MaxOutputTokens = maxOutputTokens ?? options.MaxOutputTokens)
            .Build();
        Judge = new ChatConfiguration(judgeClient);
        JudgeModel = $"{judge.Provider}:{judge.Model}";
        StoragePath = Path.Combine(RepositoryRoot(), builder.Configuration["Evaluation:StoragePath"] ?? "eval-results");
    }

    public IServiceProvider Services => host.Services;

    // The model that grades answers (LLM-as-judge). Use a stronger model than the one being judged where you
    // can: a small local judge is fine for a demo, but its scores are noisy. Avoid thinking models (qwen3.6): they
    // spend the output budget on hidden reasoning and are slow, and either way a cut-off judge leaves no score
    // ("Failed to parse score").
    public ChatConfiguration Judge { get; }

    public string JudgeModel { get; }

    // Results and cached judge responses; `dotnet aieval report` turns them into an HTML report.
    public string StoragePath { get; }

    public ReportingConfiguration CreateReporting(IEnumerable<IEvaluator> evaluators, params string[] tags) =>
        DiskBasedReportingConfiguration.Create(
            storageRootPath: StoragePath,
            evaluators: evaluators,
            chatConfiguration: Judge,
            enableResponseCaching: true,
            executionName: ExecutionName,
            tags: [.. tags, $"judge:{JudgeModel}"]);

    public void Dispose() => host.Dispose();

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (dir.GetFiles("*.slnx").Length > 0)
                return dir.FullName;
        return AppContext.BaseDirectory;
    }
}
