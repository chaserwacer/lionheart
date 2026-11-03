# Glossary

The domain's words, so the backend, the two clients and the docs use the same ones. A noun you coin
gets its line here in the same change that introduces it. If a word here is wrong, change it here
first and then in the code.

## Training

| Word | What it means |
|---|---|
| **Training session** | One bout of training. A sealed lifecycle: planned, active, completed, skipped — four shapes, not one shape with a status. |
| **Planned session** | A session that has a date it is meant for and a list of prescriptions, and no actuals. |
| **Active session** | A session in progress: it has a start instant and the sets logged so far. |
| **Completed session** | A session that happened: its sets, the period it occupied, the effort reported, and its decoration. |
| **Skipped session** | A planned session the athlete did not do. Terminal, and kept — the skip is data. |
| **Set entry** | One performed set. Per-modality shapes: lift, distance-time, trail run, freeform. |
| **Freeform set** | A set the parser could not break down. Keeps its raw words and its facets, and can be upgraded later. The words are never lost. |
| **Prescription** | A target set on a planned session — the intended reps, weight, distance or duration. |
| **Movement data** | The identity of a thing you do: a movement base plus equipment plus a modifier. Records hang off it. |
| **Movement base** | The bare exercise, before equipment: "bench press", "deadlift". |
| **Rpe** | Rate of perceived exertion for a set. Rated or unrated — never a nullable number. |
| **Perceived effort ratings** | The athlete's own read on a session, as a cluster, not loose fields. |
| **Training program** | A block of training with a title and dates. Planned, active or completed. |
| **Membership** | Whether a session stands alone or belongs to a program. A fill-state, not a nullable key. |
| **Provenance** | Whether a record came from the athlete or was assisted by the model. Orthogonal to lifecycle. |

## Body and context

| Word | What it means |
|---|---|
| **Context** | Anything outside a training session that bears on performance — a night's sleep, a stressful week, a day of pressure-washing. |
| **Exertion context** | Physical effort outside training. Carries effort ratings. |
| **State context** | A body or life state. Its valence lives in its decoration. |
| **Wellness state** | A self-reported daily reading the athlete enters. |
| **Oura day** | One day of wearable data: the six daily documents Oura files under that date — activity, readiness, sleep, SpO2, stress, resilience. |
| **Oura document** | One record as Oura issued it, keyed by Oura's own id. Either a day document or an event. |
| **Reading** | The measurement half of an Oura document — what the ring recorded, as against which day it belongs to. |
| **Sleep period** | One stretch of sleep Oura scored: a night, a nap, or rest. Zero to many per day, and the day it is filed under is the day the athlete woke. |
| **Sample run** | An evenly spaced series the ring recorded across one period — the overnight heart-rate or HRV curve. Carries its interval and start, and its gaps are real. |
| **Hypnogram** | The sleep stage per five-minute slot, kept as Oura's own digit-per-slot encoding. |
| **Readiness** | Oura's own composite score for how recovered the body is. Not Lionheart's judgement. |
| **Injury** | A tracked problem with a body part, and the events logged against it over time. |

## Analysis

| Word | What it means |
|---|---|
| **Decoration** | The annotation payload on anything analyzable: facets, free tags, valence, a summary line, and the version of the extractor that produced it. |
| **Facet** | One closed-vocabulary label on a fixed axis — the half of annotation that deterministic SQL can count. |
| **Facet axis** | The fixed set a facet's name must come from: modality, movement pattern, muscle region, intent, life domain, body state. Axes are fixed; values grow. |
| **Valence** | Whether an entry reads positive, negative, neutral or mixed. |
| **Analyzable entry** | The flat read model every analysis and retrieval query goes through: one row per completed session or context, with its facets, valence, vector and summary. |
| **Decorator version** | Which extractor produced a decoration. A row behind the current version needs re-decorating. |
| **Interpretation** | Turning raw text into domain records: parse, resolve, commit. Versioned and re-runnable. |
| **Utterance** | How an entry was logged — texted, with the raw words kept, or formed through the UI. |
| **Athlete context card** | The standing per-user summary handed to the model, so a conversation starts knowing the athlete instead of re-reading everything. |
| **Quick log** | The one-field path: type what you did in plain words and let interpretation do the rest. |

## iOS client

| Word | What it means |
|---|---|
| **Active workout** | The in-flight session as the device holds it: the sets logged so far, the rest clock, the current movement. The one piece of state the device owns. |
| **Workout bar** | The persistent accessory above the tab bar while a workout is active — this app's now-playing bar. |
| **Rest clock** | The countdown between sets. |
| **Load state** | A screen's enum for what it has: not loaded, loaded, or unavailable with a reason. Never flags plus an optional error. |
| **Set command** | A set as the athlete states it — the numbers, and nothing the server owns. Smaller than a set entry, which also carries its source and facets. |
| **Logged set** | A set command in the workout in flight, with the device's own id and the moment it was logged. Neither is sent; they exist so a row can be addressed and a rest recovered. |
| **Set modality** | Which of the four shapes a set is being logged in. A property of the set, not of the movement — the same treadmill is lifted on and run on. |
| **Workout destination** | Where a workout's sets are going to land: an active session already on the server, or a `POST /completed` that carries the whole thing at the end. |
| **Sync state** | Whether what has been logged has reached the server: synced, pending, failed, or refused. Only the last two are worth saying out loud. |
| **Logging surface** | The full-screen set-by-set screen the workout bar opens into. |
| **Profile gate** | The root's answer to whether a signed-in athlete may see the tabs: undetermined, absent, present, or unreachable. An Identity account without a Lionheart profile is refused by every training write, while reads answer empty — which is why the gate asks rather than waiting to be refused. |
| **Session log** | History's list of sessions, grouped by month, newest first. A row reads as completed, in progress, skipped, missed or planned — "missed" being a planned session whose day has gone by, which only the client can tell. |
| **Movement catalogue** | Movement names by id, so a set — which carries only a `movementDataId` — can say what it was. |
| **Coach turn** | One thing in the Coach transcript: something said, a question in flight, or a question the server did not keep because the exchange failed. |
| **Oura panel** | One collection of an Oura day as a screen shows it — its score, where Oura issues one, and the figures behind it. |
