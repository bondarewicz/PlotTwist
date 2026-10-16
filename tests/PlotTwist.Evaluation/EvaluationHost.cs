namespace PlotTwist.Evaluation;

using Application;
using Infrastructure;
using Infrastructure.Ai;
using Infrastructure.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.AI.Evaluation;
using Microsoft.Extensions.AI.Evaluation.Reporting;
using Microsoft.Extensions.AI.Evaluation.Reporting.Storage;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

public sealed class EvaluationHost : IDisposable
{
    private static readonly string ExecutionName = DateTime.UtcNow.ToString("yyyyMMdd'T'HHmmss");

    private readonly IHost host;

    public EvaluationHost()
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory }
        );
        builder.Configuration.AddUserSecrets<EvaluationHost>(optional: true);
        builder.Services.AddApplication().AddInfrastructure(builder.Configuration);
        host = builder.Build();
        host.Services.GetRequiredService<IStartupValidator>().Validate();

        var judge =
            builder.Configuration.GetSection("Evaluation:Judge").Get<ChatModelOptions>()
            ?? throw new InvalidOperationException("Evaluation:Judge is not configured.");
        // The Quality evaluators cap the judge at 800 tokens; local models get cut off before the score without this.
        var maxOutputTokens = builder.Configuration.GetValue<int?>("Evaluation:Judge:MaxOutputTokens");
        var judgeClient = AiClientFactory
            .CreateChatClient(judge)
            .AsBuilder()
            .ConfigureOptions(options => options.MaxOutputTokens = maxOutputTokens ?? options.MaxOutputTokens)
            .Build();
        Judge = new ChatConfiguration(judgeClient);
        JudgeModel = $"{judge.Provider}:{judge.Model}";
        StoragePath = Path.Combine(RepositoryRoot(), builder.Configuration["Evaluation:StoragePath"] ?? "eval-results");
    }

    public IServiceProvider Services => host.Services;

    public ChatConfiguration Judge { get; }

    public string JudgeModel { get; }

    public string StoragePath { get; }

    public ReportingConfiguration CreateReporting(IEnumerable<IEvaluator> evaluators, params string[] tags) =>
        DiskBasedReportingConfiguration.Create(
            storageRootPath: StoragePath,
            evaluators: evaluators,
            chatConfiguration: Judge,
            enableResponseCaching: true,
            executionName: ExecutionName,
            tags: [.. tags, $"judge:{JudgeModel}"]
        );

    public void Dispose() => host.Dispose();

    private static string RepositoryRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (dir.GetFiles("*.slnx").Length > 0)
                return dir.FullName;
        return AppContext.BaseDirectory;
    }
}
