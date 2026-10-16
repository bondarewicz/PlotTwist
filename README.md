# PlotTwist

*"What's that film where…?"* A small, fully local RAG demo in .NET 10. It does semantic search over about 35,000 Wikipedia film plots, using **Ollama** for embeddings and chat, **pgvector** in Docker for storage, and the **[Microsoft Agent Framework](https://learn.microsoft.com/en-us/agent-framework/overview/?pivots=programming-language-csharp)** for the agent.

It's built as four small steps. Each one runs on its own and shows one idea.

| Step | Project | What it shows |
|---|---|---|
| 0 | `src/PlotTwist.Ingest` | Load the CSV, embed each film once with `nomic-embed-text`, and upsert into pgvector. The connector creates the table and the HNSW index for you. |
| 1–2 | `src/PlotTwist.Search` | The same question answered three ways: **brute force** (`TensorPrimitives.CosineSimilarity` over every vector in memory), **pgvector** (HNSW index), and **pgvector + SQL `WHERE`** (year and genre filters in the same query). |
| 3 | `src/PlotTwist.Agent` | The same search behind an Agent Framework agent, wired two ways: a `TextSearchProvider` that retrieves before every call, or a `search_movies` tool the model calls and fills the filters for itself. |
| 4 | `tests/PlotTwist.Eval` | Recall@k and MRR over 23 "describe it without naming it" questions, so you can measure changes instead of eyeballing them. |

`src/PlotTwist.Core` holds the shared pieces: the `MoviePlot` record, CSV loading, genre normalisation, the embedder and the pgvector search.

## Run it

Prerequisites: .NET 10 SDK, Docker, and [Ollama](https://ollama.com) running natively. On macOS, Ollama inside Docker can't use the GPU.

```bash
ollama pull nomic-embed-text        # embeddings, 768 dims
ollama pull qwen3.6                 # chat, needs tool calling; any tool-capable model works

docker compose up -d --wait         # pgvector on localhost:5434

dotnet run --project src/PlotTwist.Ingest                    # downloads the CSV (~95 MB) and embeds all films (~10 min on Apple silicon)
dotnet run --project src/PlotTwist.Ingest -- --limit 5000    # or a quicker subset
```

### Step 1–2: brute force vs pgvector vs pgvector + filter

```bash
dotnet run --project src/PlotTwist.Search -- "a man relives the same day over and over"
dotnet run --project src/PlotTwist.Search -- "astronauts on a dangerous mission in space" --min-year 1990 --max-year 1999 --genre drama
```

The second query is the point of the demo. On plot alone, "astronauts in space" matches decades of sci-fi. Adding `year BETWEEN 1990 AND 1999 AND 'drama' = ANY(genres)` turns the same vector query into the answer you wanted. An in-memory list can't do that without loading everything first.

### Step 3: the agent

```bash
dotnet run --project src/PlotTwist.Agent                  # context mode: TextSearchProvider, retrieves every turn
dotnet run --project src/PlotTwist.Agent -- --mode tool   # tool mode: model decides when to search, and with which filters
```

Ask both the same things, for example *"a 90s sci-fi film where astronauts are stranded"* or *"something like Jaws but newer than 2000"*. The grey lines show what was searched. Context mode always searches, but only with your raw sentence. Tool mode extracts `minYear`/`genre` into real filters, but may skip searching.

### Step 4: eval

```bash
dotnet run --project tests/PlotTwist.Eval
```

Things to try, re-running the eval after each:
- Remove the `search_document:` / `search_query:` prefixes in `MovieEmbedder`. nomic is trained with them.
- Embed only `Summary` instead of `ToEmbeddingText()` (title, year and genres included).
- Swap `EmbeddingModel` (for example `mxbai-embed-large`, 1024 dims). Update `MoviePlot.Dimensions` and the collection name.

## Configuration

Defaults live in `PlotTwistSettings`. Override any setting with a `PLOTTWIST_` environment variable:

```bash
PLOTTWIST_ChatModel=gemma4 dotnet run --project src/PlotTwist.Agent -- --mode tool
```

## Dataset

[Wikipedia Movie Plots with summaries](https://huggingface.co/datasets/vishnupriyavr/wiki-movie-plots-with-summaries) (from [Kaggle](https://www.kaggle.com/datasets/gabrieltardochi/wikipedia-movie-plots-with-plot-summaries)), CC BY-SA 4.0, based on Wikipedia. It's downloaded into `data/` on first ingest and isn't committed. Each film has a short summary of about 380 characters, so there's one vector per film and no chunking.
