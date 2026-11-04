# lionheart.Tests

Tests for the remodeled training domain (`docs/data-remodel.md`) and the chat layer, over
both language-model providers.

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
| `Functional/` | The monad layer's own combinators — the walks and the partition |
| `ChatThread/` | The continuing chat thread: session boundaries, consolidation against a canned model, memory supersession, recall, and the athlete's corrections |
| `Llm/` | Chat and model-output contracts against both providers' faked wire APIs, plus the language-model configuration — no network |
| `Oura/` | The Oura collections: mapping, import, paging, persistence and reads (`docs/oura-integration.md`) |
| `Interpretation/` | The Parse → Resolve → Commit engine, against fakes and (live) a real model |
| `LocalModel/` | Chat-layer checks against a real LM Studio server |
| `Support/` | Fixtures: domain samples, a fake server per provider, canned responses, a test toolbox |

`Oura/Archive/` and `Oura/Damaged/` are fixture **files**, not string literals, and both are copied
to the output directory. The archive is a miniature of the athlete's own recorded Oura responses —
real bytes, so what the mapping tests assert is what a live pull would produce, which matters
because the pull itself cannot be run without his credential. `Damaged/` holds records broken on
purpose. Point the importer at a directory the way production does rather than hand-feeding it
lines; `ArchiveFixtures` resolves both paths.

`Serialization/StorageFormatTests.cs` is deliberately redundant with the round-trip tests:
round trips use the serializer as its own oracle, so a serializer-level change round-trips
happily while orphaning every stored row. The hand-written literals are the independent
oracle. If one fails, decide whether a migration is owed before updating the literal.

## The two providers

Chat runs on one of two providers, chosen by `Llm__ChatProvider` (see `.env.example`). Both are
tested the same way and for the same reason: a fake `HttpMessageHandler` under the real SDK, so
the real request building and the real response parsing stay in the path. `Support/FakeChatServer.cs`
does this for LM Studio's OpenAI-compatible API, `Support/FakeAnthropicServer.cs` for Anthropic's
Messages API, and each hands out an `IChatModel` so a consumer under test is wired exactly as the
app wires it.

**Live Anthropic calls are never made.** That needs the owner's API key, so `Llm/AnthropicChatModelTests.cs`
is the only check that the request the provider builds is the one the API documents — which is why
it asserts on the request body (no `tool_choice`, no `thinking`, a schema under `output_config`)
and not only on the answer.

## LM Studio configuration

Tests read the same keys, through the same mechanism, as `Program.cs`: `DotNetEnv` loads
`.env` into the process environment (traversing upward from the working directory), and the
environment-variable provider maps `__` to `:`. There is no parallel configuration system
and no hardcoded URL or model name in test code.

Create a `.env` at the repository root — **never commit it**, it is already gitignored.
`.env.example` is the full list; the keys the live tests need are:

```dotenv
# LM Studio local server (OpenAI-compatible API).
# BaseUrl must end at the /v1 root. Copy the model id from LM Studio's
# "Developer" / "Local Server" tab.
LmStudio__BaseUrl=http://127.0.0.1:1234/v1
LmStudio__ApiKey=your-lm-studio-api-token
LmStudio__Model=your-loaded-model-name

# Embeddings are configured independently of chat, because Anthropic serves none — so these
# keys are needed whichever provider serves chat. In development, point them at the same server.
Embedding__BaseUrl=http://127.0.0.1:1234/v1
Embedding__ApiKey=your-lm-studio-api-token
Embedding__Model=your-loaded-embedding-model-name
```

The `LmStudio__*` keys are read directly rather than through `Llm__ChatProvider`, so the
LocalModel tests target LM Studio even on a machine configured to run chat on Anthropic.

`LmStudio__ApiKey` must match the token LM Studio is actually serving with. If LM Studio has
authentication enabled and the token is stale, both `/v1/models` and `/v1/chat/completions`
answer HTTP 401 and the LocalModel tests skip with that message — the app's chat would be
equally broken, so it is worth fixing rather than working around.

`Support/LmStudio.cs` health-checks `LmStudio__BaseUrl` once per run, and if nothing answers it
tries `lms server start` and `lms load <LmStudio__Model>` before waiting up to 90s. It only ever
skips; it never fails the run for a missing server. `Support/LmStudioSettings.cs` is the single
config reader — do not read `LmStudio__*` or `Embedding__*` from the environment directly in a
test, or a missing key becomes a NullReferenceException instead of a skip that names it.

## Writing tests here

- Assert on **structure, never on prose** in LocalModel tests. Wording changes with the
  model, the quant, and the temperature; a test that breaks on different words is a broken
  test, not a finding.
- Use `Support/Some.cs` for domain samples and override with `with` — do not hand-build
  entities that the fixtures already cover.
- Tests named `*_currently_*` characterize a **known defect**: they assert what the code does
  today, not what it should do, and carry a comment saying so. When the defect is fixed, flip
  the assertion rather than deleting the test.
