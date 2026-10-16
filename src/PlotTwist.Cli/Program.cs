using System.CommandLine;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PlotTwist.Application;
using PlotTwist.Cli.Commands;
using PlotTwist.Infrastructure;

// Content root = the output folder, so appsettings.json is found whatever directory the Cli is run from.
// User secrets are loaded in every environment: provider API keys live there, not in appsettings.json.
var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
builder.Configuration.AddUserSecrets<Program>(optional: true);
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

using var host = builder.Build();
var services = host.Services;

var root = new RootCommand("PlotTwist: semantic search over ~35k Wikipedia film plots.")
{
    SearchCommands.CreateSearch(services),
    SearchCommands.CreateCompare(services),
    ChatCommand.Create(services),
};
foreach (var command in PipelineCommands.Create(services))
    root.Subcommands.Add(command);

return await root.Parse(args).InvokeAsync();
