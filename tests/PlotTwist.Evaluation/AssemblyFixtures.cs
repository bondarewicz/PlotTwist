// One EvaluationHost (and one Postgres connection pool, one set of model clients) for the whole test run.
[assembly: AssemblyFixture(typeof(PlotTwist.Evaluation.EvaluationHost))]
