namespace PlotTwist.Infrastructure.Persistence;

using System.Runtime.CompilerServices;
using Dapper;
using Pgvector.Dapper;

// Dapper's type map and handlers are global static state with no instance-scoped alternative, so they are applied
// once per process instead of as a side effect of DI registration (AddInfrastructure may never run, as in tests
// that build a NpgsqlDataSource directly).
internal static class DapperConventions
{
    // CA2255 assumes a module initializer belongs to application startup; this one is a library-internal
    // convention applied exactly once regardless of which entry point (Api, Cli, tests) loads this assembly.
#pragma warning disable CA2255
    [ModuleInitializer]
    public static void Apply()
#pragma warning restore CA2255
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new VectorTypeHandler());
    }
}
