using System.Text.Json.Serialization;
using PlotTwist.Api;
using PlotTwist.Api.Endpoints;
using PlotTwist.Application;
using PlotTwist.Infrastructure;

var builder = WebApplication.CreateBuilder(
    new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory }
);

builder
    .Services.AddApplication()
    .AddInfrastructure(builder.Configuration)
    .AddOpenApi()
    .AddProblemDetails()
    .AddExceptionHandler<DomainExceptionHandler>()
    .ConfigureHttpJsonOptions(o =>
        o.SerializerOptions.Converters.Add(
            new JsonStringEnumConverter(System.Text.Json.JsonNamingPolicy.CamelCase, allowIntegerValues: false)
        )
    );

var app = builder.Build();

app.UseExceptionHandler();
app.MapOpenApi();
app.MapMovieEndpoints();

app.Run();
