# PlotTwist

*"What's that movie where…?"* A local RAG demo in .NET 10. It does semantic search over about 35,000 Wikipedia movie plots, using **pgvector** for storage and filtering, **Microsoft.Extensions.AI** for models (Ollama by default, any OpenAI-compatible provider through configuration), and the **[Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp)** for the assistant. Retrieval and answer quality are measured with **Microsoft.Extensions.AI.Evaluation**.

## Solution layout (`PlotTwist.slnx`)

Dependencies point inwards: Domain ← Application ← Infrastructure, with Api and Cli as composition roots.

| Project | Responsibility |
|---|---|
| `src/PlotTwist.Domain` | Model and rules, no dependencies. The aggregate is `Movie`, whose identity is derived from its Wikipedia page. Value objects: `MovieId`, `ReleaseYear`, `Genre` (normalises the free-text genres), `PlotDescription`, `MovieCriteria`. `MovieEmbedding` and `EmbeddingModel` hold derived data, per model. Also `MatchConfidence` and the repository interfaces. |
| `src/PlotTwist.Application` | Use cases: the ingestion pipeline (`ImportMoviesHandler`, `EmbedMoviesHandler`, `BuildVectorIndexHandler`, `VerifyIngestionHandler`), plus `SearchMoviesHandler`, `CompareSearchStrategiesHandler` and `AskAssistantHandler`. `MovieImportPolicy` holds the dedupe and reject rules. Ports: `IMovieSource`, `IMovieStaging`, `IIngestionRunLog`, `IIngestionReport`, `IVectorIndex`, `IDatabaseMigrator`, `IPlotEmbedder`, `IMovieAssistant`, `IEmbeddingScan` (the brute-force comparison's read model). |
| `src/PlotTwist.Infrastructure` | Adapters: FluentMigrator migrations, Dapper and Npgsql repositories (binary `COPY` for bulk loads, pgvector partial HNSW indexes per model), a pinned and checksum-verified Hugging Face source, `AiClientFactory` (the only code that knows about providers), an embedder with retries (Microsoft.Extensions.Resilience), and the Agent Framework assistant. Validated `PlotTwistOptions`. |
| `src/PlotTwist.Api` | Minimal API: `POST /movies/search`, `/movies/search/compare`, `/movies/ask`, `GET /movies/stats`, plus OpenAPI at `/openapi/v1.json` |
| `src/PlotTwist.Cli` | Command line (System.CommandLine). Pipeline: `migrate`, `import`, `embed`, `index`, `verify` (or `ingest` for all of them). Querying: `search`, `compare`, `chat`. |
| `tests/PlotTwist.UnitTests` | Domain rules and use cases with in-memory fakes |
| `tests/PlotTwist.IntegrationTests` | The real migrations, repositories, staging and import against a throwaway pgvector Postgres (Testcontainers). Covers criteria → SQL, models of different dimensions side by side, and an `EXPLAIN` check that searches use the partial HNSW index. |
| `tests/PlotTwist.Evaluation` | Retrieval quality gate (recall@5, MRR) and answer quality (citations, groundedness, relevance), reported with `dotnet aieval` |

## Run it

You need the .NET 10 SDK, Docker, and [Ollama](https://ollama.com) running natively. On macOS, Ollama in Docker can't use the GPU.

```bash
ollama pull nomic-embed-text            # embeddings (768 dims)
ollama pull qwen3.6                     # chat; any model with tool calling works
docker compose up -d --wait             # pgvector on localhost:5434

dotnet run --project src/PlotTwist.Cli -- ingest               # migrate, import, embed, index, verify (~8 min on Apple silicon)
dotnet run --project src/PlotTwist.Cli -- ingest --limit 5000  # or a quicker subset
```

See [Data pipeline](#data-pipeline) for what each step does and how to run them one at a time.

### API

```bash
dotnet run --project src/PlotTwist.Api      # http://localhost:5180, requests in src/PlotTwist.Api/PlotTwist.Api.http
```

```http
POST /movies/search
{ "plot": "kids find an old treasure map and hunt for a pirate's hidden treasure while criminals chase them", "releasedFrom": 1980, "releasedTo": 1989, "genre": "adventure", "top": 3 }
```

The response contains the matches plus metadata about how they were produced:

```jsonc
{
  "query":   { "plot": "…", "criteria": { "releasedFrom": 1980, "releasedTo": 1989, "genre": "adventure", "applied": true,
               "sql": "release_year >= 1980 AND release_year <= 1989 AND genres @> ARRAY['adventure']" }, "top": 3 },
  "matches": [ { "rank": 1, "id": "a57fb8ff-99af-d14c-be61-32d9e3f21d17", "title": "Goonies, The", "releaseYear": 1985,
                 "genres": ["adventure", "family"], "origins": ["American"], "director": "Richard Donner", "plotSummary": "…",
                 "wikipediaUrl": "https://en.wikipedia.org/wiki/The_Goonies", "similarity": 0.7225 }, … ],
  "metadata": { "matchCount": 3, "embeddingModel": "nomic-embed-text", "dimensions": 768, "similarityMetric": "cosine", "indexKind": "hnsw",
                "confidence": { "topSimilarity": 0.7225, "margin": 0.0253, "ambiguous": false, "interpretation": "The top result stands out from the rest." },
                "timings": { "embeddingMs": 35.9, "searchMs": 1.8, "totalMs": 37.7 } }
}
```

- `/movies/search/compare` runs the same query three ways: **brute force** (`TensorPrimitives` over every vector in memory), the **pgvector HNSW index**, and **index + `WHERE`**. For "a hacker discovers that the world he lives in is a computer simulation" filtered to 1990s science fiction, brute force and the plain index both return *Gamer (2009)*; only the SQL filter surfaces *The Matrix (1999)*, at rank 2 just behind *The Lawnmower Man 2: Beyond Cyberspace (1996)* - a reminder that filtering helps but doesn't fix embedding-model semantics (see "Tests and evaluations" below).
- `/movies/ask` sends `{ "question": "…", "mode": "tool" | "context" }` to the Agent Framework assistant. The response includes `answer`, `searched` (whether it searched at all), the `searches` it ran with the filters *the model* chose, deduplicated `citations`, token `usage` and timing.
- Broken domain rules (an empty plot description, a year range the wrong way round) return `400` ProblemDetails.

### CLI

```bash
dotnet run --project src/PlotTwist.Cli -- search "kids find an old treasure map and hunt for a pirate's hidden treasure while criminals chase them" --released-from 1980 --released-to 1989 --genre adventure
dotnet run --project src/PlotTwist.Cli -- compare "a hacker discovers that the world he lives in is a computer simulation" --released-from 1990 --released-to 1999 --genre "science fiction"
dotnet run --project src/PlotTwist.Cli -- chat --mode tool      # or --mode context
```

## Data pipeline

Schema, source data and embeddings have different lifecycles, so each has its own step. Every step is a CLI command that can be re-run on its own:

```
migrate ─► import ─────────────────────────────────────► embed ─────────────────► index ─► verify ─► retrieval eval
 schema    pinned HF revision, sha256-checked            only new/changed movies   HNSW      counts    before switching
           → COPY into staging.movies_raw                 for the configured model  per model  add up    models
           → MovieImportPolicy → movies + movie_rejects   resumable, retried
```

| Step | What it does | Why this way |
|---|---|---|
| `migrate` | FluentMigrator migrations create `ingestion_runs`, `staging.movies_raw`, `movies` (btree on year, GIN on genres), `movie_rejects` and `movie_embeddings` | Versioned and reviewable schema. Never run implicitly at API startup. |
| `import` | Downloads `Dataset:Revision` (a commit, not `main`), verifies `Dataset:Sha256`, bulk-loads raw rows into staging with binary `COPY`, applies `MovieImportPolicy`, then upserts movies and records rejects with reasons | Reproducible input. Raw rows stay queryable. Nothing is silently dropped. It takes seconds and needs no model. |
| `embed` | Embeds movies with no embedding for the configured model, or whose `searchable_text_hash` changed. Batched (`Embeddings:BatchSize`), optionally parallel (`Embeddings:MaxConcurrency`), retried with backoff | The database is the checkpoint. Interrupt it (Ctrl+C or SIGTERM) and the next run continues. A run with nothing to do takes seconds. |
| `index` | Builds a partial HNSW index for the configured model: `((embedding::vector(N)) vector_cosine_ops) WHERE model = '…'`, with explicit `m` and `ef_construction` | Building once after a bulk load is much faster than growing the index row by row. Models with different dimensions share one table. |
| `verify` | Checks that the import accounts for every row, rejects stay under 5%, nothing is pending, and the index exists. Exits non-zero if any check fails. | The numbers must add up before anyone trusts the data. Then run the retrieval evaluation. |

New migrations use FluentMigrator's timestamp-based versioning: `[Migration(yyyyMMddHHmm, "description")]`.

**Dedupe rules** (`MovieImportPolicy`): a movie is its Wikipedia page. The source repeats pages in two ways:
- **Co-productions** listed once per country (same title and year) become one movie with several `origins`.
- **Scraping errors**, such as *The Other Side of the Door* 1916 and 2016 sharing one page and one plot. If the URL names the year (`…_(2016_film)`), that row wins. Otherwise the plot can't be attributed, and every row in the group goes to `movie_rejects`.

With the pinned revision: 34,886 rows → **33,558 movies**, **1,064 rows rejected** (3%), and 264 rows merged into co-productions.

**Switching embedding models** (blue/green): embeddings are stored per `(movie, model)`. Configure the new model in the CLI, run `embed`, `index` and `verify`, run the retrieval evaluation with the same settings, and only then point the API at it. Both models stay searchable until you delete the old rows.

**Auditing**: every run is a row in `ingestion_runs` with its source revision and checksum or model, counts and status. A run stopped with Ctrl+C or SIGTERM is recorded with `status = 'cancelled'` and whatever counts it reached (e.g. `embedded` short of `read`), not as a failure.

## Configuration

Everything about models, providers and prompts lives in the `PlotTwist` section of the repo-root `appsettings.json`, shared by the Api, Cli and Evaluation project (linked into each one's build output via `Directory.Build.props`, so there is one file to edit instead of three copies to keep in sync). Nothing provider- or prompt-specific is in code. Options are validated at startup with the source-generated `[OptionsValidator]`, so a broken configuration fails immediately.

| Section | What |
|---|---|
| `Database` | Connection string (keep `Options=-c hnsw.iterative_scan=relaxed_order -c hnsw.ef_search=100`, see below), plus HNSW build settings: `HnswM`, `HnswEfConstruction`, `IndexBuildMemory`, `IndexBuildWorkers` |
| `Embeddings` | `Provider`, `Model`, `Endpoint`, `ApiKey`, `Dimensions`, `DocumentPrefix`/`QueryPrefix` for models trained with task prefixes such as nomic, and the embed job's `BatchSize`/`MaxConcurrency` |
| `Chat` | `Provider`, `Model`, `Endpoint`, `ApiKey` |
| `Assistant` | System `Instructions`, `ToolInstructions`, the context-mode `ContextPrompt`, `MatchesPerSearch` (10: on the retrieval evaluation the right movie is in the top 5 for 61% of cases and the top 10 for 78%), and the search tool's name, description and parameter descriptions |
| `Dataset` | Hugging Face `Repository`, `Revision` (a commit SHA), `File`, `Sha256`, `Licence`, `CacheDirectory`. CLI only. |

Providers are `Ollama` and `OpenAI`. With `Endpoint` set, `OpenAI` works with any OpenAI-compatible server: Azure OpenAI (`https://<resource>.openai.azure.com/openai/v1/`), LM Studio, vLLM, Groq, OpenRouter and others. Keep keys out of `appsettings.json`:

```bash
dotnet user-secrets set PlotTwist:Chat:ApiKey sk-... --project src/PlotTwist.Api   # shared UserSecretsId with Cli and Evaluation
PlotTwist__Chat__Provider=OpenAI PlotTwist__Chat__Model=gpt-4.1-mini dotnet run --project src/PlotTwist.Api
```

Changing the embedding model changes the vectors. Set `Model` and `Dimensions`, then run `embed`, `index` and `verify`. The new model's vectors sit alongside the old ones (see [Data pipeline](#data-pipeline)).

### Two pgvector settings that matter

- **`hnsw.iterative_scan=relaxed_order`** (pgvector 0.8+). Without it, HNSW returns about 40 candidates and the `WHERE` clause runs afterwards, so a selective filter returns fewer rows than you asked for. During development, `top: 3` with a 90s-drama filter returned 2.
- **`hnsw.ef_search=100`** (default 40). This widens the candidate list. The retrieval evaluation measured recall@5 going from 57% to 61%, and 200 added nothing.

## Development

Package versions are centralised in `Directory.Packages.props` (`ManagePackageVersionsCentrally`); `Directory.Build.props` sets the shared `TargetFramework`, `Nullable` and `TreatWarningsAsErrors` for every project. `.editorconfig` fixes conventions CSharpier doesn't own (LF line endings, `using` directives inside the namespace). Format before committing:

```bash
dotnet tool restore
dotnet csharpier format .
```

## Tests and evaluations

```bash
dotnet test --project tests/PlotTwist.UnitTests            # fast, no infrastructure
dotnet test --project tests/PlotTwist.IntegrationTests     # needs Docker (Testcontainers)

# Needs the ingested database and the configured models
dotnet test --project tests/PlotTwist.Evaluation -- --filter-trait "Category=Retrieval"   # deterministic, seconds
dotnet test --project tests/PlotTwist.Evaluation -- --filter-trait "Category=Answers"     # chat model + judge model, minutes

dotnet tool restore
dotnet aieval report --path eval-results --output eval-report.html                      # HTML report, compares runs
```

The tests run on Microsoft.Testing.Platform (`global.json`) with xUnit v3.

Evaluations are tests, but they're built with [Microsoft.Extensions.AI.Evaluation](https://learn.microsoft.com/dotnet/ai/evaluation/libraries) rather than plain asserts:

- **Retrieval** (`RankEvaluator`, deterministic): 23 well-known movies described without their names. Each case is recorded as a scenario in the report, and the build gates on the **aggregate** recall@5. The gate is a ratchet: raise it as retrieval improves, never lower it to make a change pass. Current baseline with nomic-embed-text: recall@1 35%, recall@5 61%, MRR 0.46. Most misses are the embedding model's semantics: *Inception* ("thieves… dreams") ranks below movies with "Thief" and "Steal" in the title, even with exact brute-force search.
- **Answers**: `CitedMoviesEvaluator` (deterministic) checks that every "Title (Year)" in the answer came from a search. `GroundednessEvaluator` and `RelevanceEvaluator` are LLM-as-judge, scored 1–5. The judge is configured under `Evaluation:Judge` in the evaluation project. Use a stronger model than the one being judged where you can. Judge responses are cached, so reruns are cheap.
  - With a local judge (gemma4 by default), expect some unscored judgements. Local models write long rationales and don't always end in the score format the evaluators parse. `Evaluation:Judge:MaxOutputTokens` raises the evaluators' 800-token cap, and an unscored judgement is reported as a **skip**, not a failure, because it says nothing about the answer. Thinking models such as qwen3.6 make poor judges: they spend the budget on hidden reasoning.
  - The context vs tool trade-off shows up here. For "a 90s sci-fi film where astronauts are stranded", context mode searches with the raw sentence and no year filter (Relevance 2–3). Tool mode sets `1990–1999 + science fiction` itself (Relevance 4–5).
  - The evaluations have already caught two bugs: a citation parser that flagged Agent Framework's `[SourceDocName: …]` citations as hallucinations, and tool arguments such as `"releasedFrom": "1990s"` that failed the whole `/movies/ask` request. Both are fixed and covered by tests.

## Dataset

[Wikipedia Movie Plots with summaries](https://huggingface.co/datasets/vishnupriyavr/wiki-movie-plots-with-summaries), CC BY-SA 4.0, derived from Wikipedia. It's pinned to revision `a1feef07` (sha256 `c01cf576…`), downloaded into `data/` on first `import`, and not committed. Each summary is about 380 characters, so each movie is one vector and there's no chunking. `Plot` (the full text) and `Cast` are kept in staging but not used yet, so using them later is a transform change, not a re-download.
