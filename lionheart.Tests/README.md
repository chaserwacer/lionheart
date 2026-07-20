# lionheart.Tests

Tests for the remodeled training domain (`docs/data-remodel.md`) and the LM Studio chat layer.

## Running

```bash
# Everything except the local-model integration tests. This is the default:
# lionheart.Tests.csproj points RunSettingsFilePath at ../lionheart.runsettings,
# which filters out Category=LocalModel.
dotnet test

# Only the live LM Studio tests (chat layer + interpretation engine).
dotnet test --settings lionheart.localmodel.runsettings

# Everything, mocked and local-model alike.
dotnet test --settings lionheart.all.runsettings
```

**Why `--filter "Category=LocalModel"` does not work.** VSTest **ANDs** a command-line
`--filter` with the runsettings `TestCaseFilter`, so it resolves to
`(Category!=LocalModel)&(Category=LocalModel)` — an empty set, and `dotnet test` reports
"No test matches". Selecting a different settings file replaces the default outright, which
is why the two extra `.runsettings` files exist. `--filter` still works for narrowing within
a run, e.g. `dotnet test --filter "FullyQualifiedName~ChatCompletionServiceTests"`.

**Live tests use one mechanism, and it has two halves that do different jobs.** *Selection* is
the `[Trait("Category", "LocalModel")]` attribute on the test class, filtered out by the default
runsettings — that is what keeps a plain run hermetic and fast, and why nothing probes the
network at discovery time. *Skipping* is `[LmStudioFact]` plus `LmStudioFactAttribute.SkipUnlessAvailable()`,
called before touching a client: once selected, a test that cannot reach a server reports as
**skipped** with the failing key or step named, never as a failure. Add both to any new live
test; do not introduce a third gate.

## Layout

| Folder | What it covers |
|---|---|
| `Domain/` | Invariants of the sealed hierarchies — no I/O |
| `Serialization/` | The JSON storage format: round trips, hand-written literal fixtures, malformed payloads |
| `Persistence/` | Real in-memory SQLite through the full `ModelContext` |
| `Llm/` | Chat and model-output contracts against a faked LM Studio — no network |
| `Interpretation/` | The Parse → Resolve → Commit engine, against fakes and (live) a real model |
| `LocalModel/` | Chat-layer checks against a real LM Studio server |
| `Support/` | Fixtures: domain samples, the fake chat server, canned responses, a test toolbox |

`Serialization/StorageFormatTests.cs` is deliberately redundant with the round-trip tests:
round trips use the serializer as its own oracle, so a serializer-level change round-trips
happily while orphaning every stored row. The hand-written literals are the independent
oracle. If one fails, decide whether a migration is owed before updating the literal.

## LM Studio configuration

Tests read the same keys, through the same mechanism, as `Program.cs`: `DotNetEnv` loads
`.env` into the process environment (traversing upward from the working directory), and the
environment-variable provider maps `__` to `:`. There is no parallel configuration system
and no hardcoded URL or model name in test code.

Create a `.env` at the repository root — **never commit it**, it is already gitignored:

```dotenv
# LM Studio local server (OpenAI-compatible API).
# BaseUrl must end at the /v1 root. Copy the model id from LM Studio's
# "Developer" / "Local Server" tab.
LmStudio__BaseUrl=http://127.0.0.1:1234/v1
LmStudio__ApiKey=your-lm-studio-api-token
LmStudio__Model=your-loaded-model-name
LmStudio__EmbeddingModel=your-loaded-embedding-model-name
```

`LmStudio__ApiKey` must match the token LM Studio is actually serving with. If LM Studio has
authentication enabled and the token is stale, both `/v1/models` and `/v1/chat/completions`
answer HTTP 401 and the LocalModel tests skip with that message — the app's chat would be
equally broken, so it is worth fixing rather than working around.

`Support/LmStudio.cs` health-checks `LmStudio__BaseUrl` once per run, and if nothing answers it
tries `lms server start` and `lms load <LmStudio__Model>` before waiting up to 90s. It only ever
skips; it never fails the run for a missing server. `Support/LmStudioSettings.cs` is the single
config reader — do not read `LmStudio__*` from the environment directly in a test, or a missing
key becomes a NullReferenceException instead of a skip that names it.

## Writing tests here

- Assert on **structure, never on prose** in LocalModel tests. Wording changes with the
  model, the quant, and the temperature; a test that breaks on different words is a broken
  test, not a finding.
- Use `Support/Some.cs` for domain samples and override with `with` — do not hand-build
  entities that the fixtures already cover.
- Tests named `*_currently_*` characterize a **known defect**: they assert what the code does
  today, not what it should do, and carry a comment saying so. When the defect is fixed, flip
  the assertion rather than deleting the test.
