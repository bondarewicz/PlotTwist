namespace PlotTwist.Infrastructure.Persistence;

using System.Runtime.CompilerServices;
using Dapper;
using Pgvector.Dapper;

internal static class DapperConventions
{
#pragma warning disable CA2255 // not application startup, but still exactly-once library-internal setup
    [ModuleInitializer]
    public static void Apply()
#pragma warning restore CA2255
    {
        DefaultTypeMap.MatchNamesWithUnderscores = true;
        SqlMapper.AddTypeHandler(new VectorTypeHandler());
    }
}
