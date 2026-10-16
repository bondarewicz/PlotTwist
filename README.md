# PlotTwist

*"What's that film where…?"* A local RAG demo in .NET 10. It does semantic search over about 35,000 Wikipedia film plots, using **pgvector** for storage and filtering, **Microsoft.Extensions.AI** for models (Ollama by default, any OpenAI-compatible provider through configuration), and the **[Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp)** for the assistant. Retrieval and answer quality are measured with **Microsoft.Extensions.AI.Evaluation**.

## Solution layout (`PlotTwist.slnx`)

Dependencies point inwards: Domain ← Application ← Infrastructure, with Api and Cli as composition roots.

| Project | Responsibility |
|---|---|
| `src/PlotTwist.Domain` | Model and rules, no dependencies. The aggregate is `Movie`. Value objects: `MovieId`, `ReleaseYear`, `Genre` (normalises the free-text genres), `PlotDescription`, `MovieCriteria`, `Embedding`. Also `MatchConfidence` and the `IMovieRepository` interface. |
| `src/PlotTwist.Application` | Use cases: `IngestMoviesHandler`, `FindMoviesHandler`, `CompareSearchStrategiesHandler`, `AskAssistantHandler`. Ports to the outside world: `IPlotEmbedder`, `IMovieDataset`, `IStorageInitializer`, `IMovieAssistant`. |
| `src/PlotTwist.Infrastructure` | Adapters: a pgvector repository (CommunityToolkit.VectorData.PgVector, with its own `MovieRecord` persistence model), `AiClientFactory` (the only code that knows about providers), the embedder, the CSV dataset, and the Agent Framework assistant. Validated `PlotTwistOptions`. |
| `src/PlotTwist.Api` | Minimal API: `POST /movies/search`, `/movies/search/compare`, `/movies/ask`, `GET /movies/stats`, plus OpenAPI at `/openapi/v1.json` |
| `src/PlotTwist.Cli` | Command line (System.CommandLine): `ingest`, `search`, `compare`, `chat` |
| `tests/PlotTwist.UnitTests` | Domain rules and use cases with in-memory fakes |
| `tests/PlotTwist.IntegrationTests` | The pgvector repository against a throwaway Postgres (Testcontainers): table creation, criteria → SQL, ordering |
| `tests/PlotTwist.Evaluation` | Retrieval quality gate (recall@5, MRR) and answer quality (citations, groundedness, relevance), reported with `dotnet aieval` |

## Run it

You need the .NET 10 SDK, Docker, and [Ollama](https://ollama.com) running natively. On macOS, Ollama in Docker can't use the GPU.

```bash
ollama pull nomic-embed-text            # embeddings (768 dims)
ollama pull qwen3.6                     # chat; any model with tool calling works
docker compose up -d --wait             # pgvector on localhost:5434

dotnet run --project src/PlotTwist.Cli -- ingest               # downloads ~95 MB and embeds 34,886 films (~10 min on Apple silicon)
dotnet run --project src/PlotTwist.Cli -- ingest --limit 5000  # or a quicker subset
```

### API

```bash
dotnet run --project src/PlotTwist.Api      # http://localhost:5180, requests in src/PlotTwist.Api/PlotTwist.Api.http
```

```http
POST /movies/search
{ "description": "astronauts on a dangerous mission in space", "releasedFrom": 1990, "releasedTo": 1999, "genre": "drama", "top": 3 }
```

The response contains the results plus metadata about how they were produced:

```jsonc
{
  "query":   { "description": "…", "criteria": { "releasedFrom": 1990, "releasedTo": 1999, "genre": "drama", "applied": true,
               "filter": "\"Year\" >= 1990 AND \"Year\" <= 1999 AND 'drama' = ANY(\"Genres\")" }, "top": 3 },
  "results": [ { "rank": 1, "id": 12362, "title": "Apollo 13", "year": 1995, "genres": ["drama", "biography"], "origin": "American",
                 "director": "Ron Howard", "summary": "…", "wikipediaUrl": "https://en.wikipedia.org/wiki/Apollo_13_(film)", "similarity": 0.6889 }, … ],
  "metadata": { "resultCount": 3, "embeddingModel": "nomic-embed-text", "dimensions": 768, "distance": "cosine", "index": "hnsw",
                "confidence": { "topSimilarity": 0.6889, "margin": 0.0007, "ambiguous": true, "interpretation": "Ambiguous: …" },
                "timings": { "embeddingMs": 83.8, "searchMs": 36.5, "totalMs": 120.3 } }
}
```

- `/movies/search/compare` runs the same query three ways: **brute force** (`TensorPrimitives` over every vector in memory), the **pgvector HNSW index**, and **index + `WHERE`**. With the request above, the first two both return *Dangerous Mission (1954)*, a match on the title words. Only the SQL filter finds *Apollo 13*.
- `/movies/ask` sends `{ "question": "…", "mode": "tool" | "context" }` to the Agent Framework assistant. The response includes `answer`, `grounded` (whether it searched at all), the `searches` it ran with the filters *the model* chose, deduplicated `sources`, token `usage` and timing.
- Broken domain rules (an empty description, a year range the wrong way round) return `400` ProblemDetails.

### CLI

```bash
dotnet run --project src/PlotTwist.Cli -- search "a robot falls in love" --from 2000 --to 2010 --genre animated
dotnet run --project src/PlotTwist.Cli -- compare "astronauts on a dangerous mission in space" --from 1990 --to 1999 --genre drama
dotnet run --project src/PlotTwist.Cli -- chat --mode tool      # or --mode context
```

## Configuration

Everything about models, providers and prompts lives in the `PlotTwist` section of each host's `appsettings.json`. Nothing provider- or prompt-specific is in code. Options are validated at startup with the source-generated `[OptionsValidator]`, so a broken configuration fails immediately.

| Section | What |
|---|---|
| `Database` | Connection string and collection. Keep `Options=-c hnsw.iterative_scan=relaxed_order -c hnsw.ef_search=100` (see below). |
| `Embeddings` | `Provider`, `Model`, `Endpoint`, `ApiKey`, `Dimensions`, plus `DocumentPrefix`/`QueryPrefix` for models trained with task prefixes, such as nomic |
| `Chat` | `Provider`, `Model`, `Endpoint`, `ApiKey` |
| `Assistant` | System `Instructions`, `ToolInstructions`, the context-mode `ContextPrompt`, `ResultsPerSearch`, and the search tool's name, description and parameter descriptions |
| `Dataset` | CSV URL and local path (CLI only) |

Providers are `Ollama` and `OpenAI`. With `Endpoint` set, `OpenAI` works with any OpenAI-compatible server: Azure OpenAI (`https://<resource>.openai.azure.com/openai/v1/`), LM Studio, vLLM, Groq, OpenRouter and others. Keep keys out of `appsettings.json`:

```bash
dotnet user-secrets set PlotTwist:Chat:ApiKey sk-... --project src/PlotTwist.Api   # shared UserSecretsId with Cli and Evaluation
PlotTwist__Chat__Provider=OpenAI PlotTwist__Chat__Model=gpt-4.1-mini dotnet run --project src/PlotTwist.Api
```

Changing the embedding model changes the vectors: set `Dimensions`, use a new `Database:Collection`, re-run `ingest`, then run the retrieval evaluation to see whether it helped.

### Two pgvector settings that matter

- **`hnsw.iterative_scan=relaxed_order`** (pgvector 0.8+). Without it, HNSW returns about 40 candidates and the `WHERE` clause runs afterwards, so a selective filter returns fewer rows than you asked for. During development, `top: 3` with a 90s-drama filter returned 2.
- **`hnsw.ef_search=100`** (default 40). This widens the candidate list. The retrieval evaluation measured recall@5 going from 57% to 61%, and 200 added nothing.

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

- **Retrieval** (`RankEvaluator`, deterministic): 23 well-known films described without their names. Each case is recorded as a scenario in the report, and the build gates on the **aggregate** recall@5. The gate is a ratchet: raise it as retrieval improves, never lower it to make a change pass. Current baseline with nomic-embed-text: recall@1 35%, recall@5 61%, MRR 0.46. Most misses are the embedding model's semantics: *Inception* ("thieves… dreams") ranks below films with "Thief" and "Steal" in the title, even with exact brute-force search.
- **Answers**: `CitedFilmsEvaluator` (deterministic) checks that every "Title (Year)" in the answer came from a search. `GroundednessEvaluator` and `RelevanceEvaluator` are LLM-as-judge, scored 1–5. The judge is configured under `Evaluation:Judge` in the evaluation project. Use a stronger model than the one being judged where you can. Judge responses are cached, so reruns are cheap.
  - With a local judge (gemma4 by default), expect some unscored judgements. Local models write long rationales and don't always end in the score format the evaluators parse. `Evaluation:JudgeMaxOutputTokens` raises the evaluators' 800-token cap, and an unscored judgement is reported as a **skip**, not a failure, because it says nothing about the answer. Thinking models such as qwen3.6 make poor judges: they spend the budget on hidden reasoning.
  - The context vs tool trade-off shows up here. For "a 90s sci-fi film where astronauts are stranded", context mode searches with the raw sentence and no year filter (Relevance 2–3). Tool mode sets `1990–1999 + science fiction` itself (Relevance 4–5).
  - The evaluations have already caught two bugs: a citation parser that flagged Agent Framework's `[SourceDocName: …]` citations as hallucinations, and tool arguments such as `"releasedFrom": "1990s"` that failed the whole `/movies/ask` request. Both are fixed and covered by tests.

## Dataset

[Wikipedia Movie Plots with summaries](https://huggingface.co/datasets/vishnupriyavr/wiki-movie-plots-with-summaries), CC BY-SA 4.0, derived from Wikipedia. It's downloaded into `data/` on first ingest and isn't committed. Each summary is about 380 characters, so each film is one vector and there's no chunking.
