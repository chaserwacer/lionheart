# Quick Log — the flagship interaction

## Why this exists

The owner does not use Lionheart. Not because it lacks features — because it lives on a laptop and
logging a session takes a form. The interpretation engine (`Services/Interpretation/`) already turns

> "sled pull+push worked up to 235, 2 or 3 sets, shoulder felt tweaky"

into a decorated, faceted, embedded `CompletedTrainingSession` plus its `AnalyzableEntry` projection.

Everything needed for one-tap logging is built. What is missing is a surface. **On a phone, that
surface is a text box and a microphone button** — and it is simultaneously the most compelling thing
in the app to demonstrate. Those two facts coinciding is why this is the priority, not the polish.

## The interaction

1. **Open** — the app opens on Quick Log. Not a dashboard, not a nav menu. One focused input.
2. **Speak or type** — free text. `SpeechRecognition` where available (Safari/iOS supports
   `webkitSpeechRecognition`), with the text box always the fallback. Never voice-only.
3. **Submit** → `POST /api/interpretation/interpret`.
4. **Two outcomes**, and the design lives or dies on how the second one feels:
   - `Committed` — show what was understood as a compact card (movements, sets, the facets it
     assigned) with a single **Undo**. Do not make the athlete confirm a correct parse.
   - `AwaitingConfirmation` — the parser met a name it could not resolve. Show *only* the ambiguous
     names, each with its ranked candidates plus "create new". One tap each, then
     `POST /api/interpretation/confirm` with the echoed draft. **Never re-ask for the whole entry**,
     and never re-parse — the draft round-trips precisely so the model call is not paid twice.
5. **Optimistic and offline-tolerant.** The utterance is written to local storage the instant it is
   submitted and cleared only on a committed response. A failed or offline submit leaves a pending
   entry the athlete can retry — losing a logged set to a dropped connection is the one failure this
   feature cannot have.

## What it must not become

- A form with a text box bolted on. If a field appears, it belongs in the structured editor instead.
- A chat. This is a logging surface with a parse step, not a conversation — there is no history, no
  turn-taking, and no assistant persona. Chat already exists at `/chat`.
- Blocking on the LLM. The parse takes as long as it takes on a local model; the UI shows honest
  progress and stays interruptible.

## Relationship to structured entry

Both write paths are first class (owner call). Quick Log is the *default*; the structured
session editor remains for planning a program, editing an existing session, and correcting a parse.
A parse produces a real `CompletedTrainingSession`, so the editor opens on it with no conversion.
