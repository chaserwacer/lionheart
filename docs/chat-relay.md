# Chat relay: testing the chat by playing the model

The relay is a development-only chat provider. Each request the chat would send to Anthropic is
written to a folder, and whoever is testing (a Claude Code session, usually) reads it, answers as the
model would, and writes the answer back as a file. Everything else runs for real: the context card,
the TODAY briefing, memory recall and consolidation, the tool registry and the tool loop, the cache
breakpoints, and the Anthropic provider's own request building and response parsing.

Use it for any chat testing that should look like production. It costs nothing, and it never uses the
owner's API key. LM Studio (`Llm__ChatProvider=LmStudio`) is the free fallback when an unattended
model is enough for a quick check; it answers on its own, but it is a different model with a
different prompt layout (see "Where the briefing goes" below).

## Running it

```bash
ASPNETCORE_ENVIRONMENT=Development Llm__ChatProvider=Relay Relay__Folder=/path/to/scratch/chat-relay \
  dotnet run --no-launch-profile --urls http://localhost:<your port>
```

- **Development only.** The host refuses to start with `Relay` in any other environment, naming the
  key. It is never the default; an unset `Llm__ChatProvider` means LM Studio.
- **Variables set on the process win over `.env`.** The main checkout's `.env` names Anthropic, and
  it is loaded with `NoClobber`, so the command above runs on the relay. A worktree can also keep
  its own `.env` with only the `LmStudio__*` and `Embedding__*` keys. The search for `.env` stops at
  the first file it finds, so the main checkout's file is then never read.
- **The folder** defaults to `lionheart-chat-relay` in the system temp directory. Keep it outside the
  repository: every request holds the athlete's real data.
- Embeddings still come from the `Embedding__*` server, because memory recall needs vectors.

## The exchange

For each request the relay writes two files and waits.

| File | What it is |
|---|---|
| `0007.request.json` | The exact Messages API body production would send |
| `0007-chat.prompt.md` | The same request laid out for reading: estimated tokens by section, where the cache breakpoints are, the system prompt, every message (tool calls and results included) and the tools on offer |

The label after the number says which caller is asking:
- `chat` is a turn of a conversation, with tools on offer.
- `extract` is a schema-bound extraction, such as memory consolidation or log interpretation. Its
  readable file ends with the answer's schema.
- `prose` is a one-sentence card narrative.

Answer with one of these:

| File | Use it for |
|---|---|
| `0007.response.txt` | A plain answer. The text becomes the turn's single text block. For an `extract`, the JSON goes here. |
| `0007.response.json` | Anything else, in the Messages API response shape. Usually only `content` is needed. |

```json
{"content":[{"type":"tool_use","name":"GetTrainingScore","input":{"From":"2026-01-05","To":"2026-01-11"}}]}
```

The relay fills in whatever is left out: `id`, `role`, `model`, an id for each `tool_use`, the stop
reason (`tool_use` when there is a call, otherwise `end_turn`), and estimated `usage`. Anything you
do write is kept, so `"stop_reason":"max_tokens"` exercises that failure. A tool call comes back as
the next numbered request, with the tool's result in it.

### Streaming

Chat turns ask to stream (`"stream": true` in the request file), as production does. The relay
answers them with the same file as any other request, and plays it back as the Messages API's event
stream: the answer's text a word at a time, at `Relay__WordsPerSecond` (25 by default), thinking and
tool calls in one piece each. So a reply arrives on the phone the way Claude's would, however fast
the file was written. Set the pace low (`Relay__WordsPerSecond=3`) to have time to watch a reply
arrive, or to drop the connection partway through one: the server finishes and stores it anyway,
and the app finds it in the thread when it returns. Extractions and narratives do not stream.

A request waits as long as its caller does. The iOS app follows a thread reply for as long as it
takes, since the server's heartbeat keeps the stream open, but a standalone conversation waits five
minutes (`HTTP.modelPatience`), so answer within that when driving one; `curl --max-time 3600` gives
an hour. Card narratives and session consolidation go through the relay too. A consolidation runs
under the thread's lock, so a chat message waits behind an unanswered `extract`.

## Driving it from Claude Code

1. Start the server as above in the background. Copy the snapshot database into the worktree's
   `Data/` and set a throwaway password on that copy only.
2. Send a message (`POST /api/chat/thread/messages` with a bearer token, or
   `/api/chat/thread/messages/stream` with `curl -N` to watch the events) in the background.
3. Wait for the next numbered `*.prompt.md`. Read it, and answer the way Claude would with exactly
   that context. That means making the tool calls a careful model would make, not the ones you know
   to be needed.
4. Repeat until the reply comes back.

A `Propose…` tool call is answered like any other: its result tells you what was put on a card, and
nothing is written. The card is answered from the app's Chat tab, or with
`POST /api/chat/proposals/{id}/confirm` or `/decline`; the next message's request then says, after the
briefing, where each proposal of the session stands.

To cross the memory gap without waiting three hours, move the newest session's `LastUpdate` back
four hours in the copied database. The next message then consolidates that session (an `extract`
request) and opens a new one with the memories recalled.

## Measuring

Every readable request starts with its estimated size at four characters a token:
`tools 2171 · system 1906 · messages 748 (system notes 717) · total 4825`. Count the `chat` requests
per question to get the rounds and tool calls it took. Compare these figures between versions of the
prompt, not against a bill.

The chat requests show the cache layout too:
- The system prompt is frozen for a session.
- The athlete's latest words carry the hour-long breakpoint.
- The briefing comes after them as a `system` message, and it is never replayed.

Each request's history therefore matches the previous request's cached prefix up to the words.

## Where the briefing goes

The TODAY briefing (`Services/Chat/Today`) is placed differently by each provider:
- **Anthropic and the relay:** a mid-conversation `system` message after the latest user words.
- **LM Studio:** appended to the system prompt. Local chat templates reject a system message
  anywhere but first, and missing the local prefix cache costs nothing.
