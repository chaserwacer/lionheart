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
| **Strong movement map** | The athlete-editable file saying which movement each exercise name in a Strong export becomes. |
| **Rpe** | Rate of perceived exertion for a set. Rated or unrated — never a nullable number. |
| **Perceived effort ratings** | The athlete's own read on a session, as a cluster, not loose fields. |
| **Training program** | A block of training with a title and dates. Planned, active or completed. |
| **Membership** | Whether a session stands alone or belongs to a program. A fill-state, not a nullable key. |
| **Provenance** | Whether a record came from the athlete or was assisted by the model. Orthogonal to lifecycle. |
| **Personal record** | The best an athlete has done at one thing: a subject, a kind, and the performance holding it. Established when a session's sets become final, never edited afterwards — only withdrawn. |
| **Record subject** | What a record is held against: one movement data, or trail running, which names no movement. |
| **Record kind** | Which question a record answers — heaviest set, best set volume, rep max at a rep count, best session volume, longest distance, fastest time at a distance, best pace. |
| **Achievement** | A record's kind fused with the performance that holds it, so a heaviest-set record cannot hold a distance. |
| **Rep max** | The heaviest weight lifted for a rep count, in a set of that many reps or more: a set of seven holds the six-rep max too, because its first six reps were performed. Kept from two to twelve reps; the one-rep max is the heaviest set. |
| **Rep-max grid** | A movement's best weight at each rep count from one to twelve, the first cell being the heaviest set. The thing most lifters actually train against. |
| **Record standing** | Whether a record is the current one, one the athlete beat, or one they withdrew. Three states, because "beaten" and "withdrawn" read nothing alike. |
| **Record chain** | Every record of one identity — subject, kind and weight unit — newest first: the one standing, the ones it beat, and any withdrawn. What a record's history shows. |
| **Record lineage** | What a record replaced — nothing, for the first of its kind — carrying the predecessor's date so "how long the last one stood" answers without a second read. |
| **Retracting a record** | Withdrawing the standing record because the set behind it was not real, which puts back whatever it beat. The only edit an athlete makes to a record. |
| **Estimated one-rep max** | A single rep implied by a set, by Epley. Derived from the stored weight and reps on the way out, never stored. |
| **Established records** | Every record a movement or a window of time set, whatever became of each since — standing, beaten or withdrawn. What marks the sets that set a record. |
| **Focus pin** | A movement the athlete has pinned to emphasise over a horizon, at a number of sessions a week, perhaps toward a target. Pinned or unpinned; an unpinned pin is kept as history. |
| **Focus terms** | What a focus pin asks for: its horizon, its sessions a week and its target. Revised whole, and only while the pin is pinned. |
| **Focus horizon** | The days a focus pin covers, first and last included. Whether it has begun or passed is read against today, never stored. |
| **Focus target** | No target, or a lift target — a weight in its unit for a rep count. Whether it was met is read against the training log, never stored. |
| **Unpinning** | Letting a focus go, on a day the athlete names. Once only; delete is for a pin made by mistake. |

## Body and context

| Word | What it means |
|---|---|
| **Context** | Anything outside a training session that bears on performance — a night's sleep, a stressful week, a day of pressure-washing. |
| **Exertion context** | Physical effort outside training. Carries effort ratings. |
| **State context** | A body or life state. Its valence lives in its decoration. |
| **Wellness state** | A self-reported daily reading the athlete enters. |
| **Oura day** | One day of wearable data: the eight daily documents Oura files under that date — activity, readiness, sleep, SpO2, stress, resilience, VO2max, cardiovascular age. |
| **Oura document** | One record as Oura issued it, keyed by Oura's own id. Either a day document or an event. |
| **Reading** | The measurement half of an Oura document — what the ring recorded, as against which day it belongs to. |
| **Sleep period** | One stretch of sleep Oura scored: a night, a nap, or rest. Zero to many per day, and the day it is filed under is the day the athlete woke. |
| **Sample run** | An evenly spaced series the ring recorded across one period — the overnight heart-rate or HRV curve. Carries its interval and start, and its gaps are real. |
| **Hypnogram** | The sleep stage per five-minute slot, kept as Oura's own digit-per-slot encoding: 1 deep, 2 light, 3 REM, 4 awake. |
| **Activity timeline** | The activity class of every five-minute slot of an Oura activity day, which runs 04:00 to 04:00 on the athlete's clock. Kept as Oura's digits. |
| **Activity class** | What Oura called one slot of the day: non-wear, rest, inactive, low, medium or high. |
| **Sitting** | A waking run of inactive slots and the brief rest inside them, at least 30 minutes long, ended by any slot of movement, by the ring coming off, by sleep, or by lying still. A *long* sitting is an hour or more. (A *sedentary bout* in the code.) |
| **Lying still** | Half an hour or more of rest outside a sleep Oura scored — an unscored nap or night, or the ring on a table. Not sitting. |
| **Complete day** | An activity day with a timeline and no more than three hours off the finger or lying still. Only complete days count toward a habits summary. |
| **Habits report** | How the athlete's days are shaped over a span — sittings, movement and training by hour, steps, sleep timing — split into weekdays and weekends and set against the equal span before. |
| **Heart-rate series** | Oura's all-day heart rate: samples about every five minutes, faster in workouts, each with what the ring was doing. Stored as one run per UTC day. |
| **Cardio session** | A logged run, or a ring workout that is endurance, a sport, or a walk of 30 minutes or more, and is not inside a logged session. |
| **Maximum heart rate** | What the zones are drawn from: the higher of the age prediction and the ring's own cardio peak (its 99th percentile over a year). |
| **Heart-rate zones** | Five bands at 50, 60, 70, 80 and 90 per cent of maximum heart rate. Zone 2 is 60–70 per cent. |
| **Cardio load** | Edwards' training impulse: minutes in each zone times the zone's number, summed. |
| **Aerobic base** | Whether cardio fitness is moving: zone-2 and cardio minutes per week, VO2max, the nights' lowest heart rate and cardiovascular age, over the last four weeks against the eight before. |
| **VO2max estimate** | Oura's estimate of maximal oxygen uptake from heart rate and walking. An estimate, not a test; read for its trend. |
| **Cardiovascular age** | Oura's predicted vascular age. A model output; read for its trend. |
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
| **Active workout** | The in-flight session as the device holds it: the sets logged so far, the rest clock, the current movement, the movements the athlete added. The one piece of state the device owns. |
| **Added movement** | A movement the athlete chose for the workout in flight. It has a section on the logging surface before any set is logged on it, which is how an unplanned workout logs its first lift. |
| **Workout bar** | The persistent accessory above the tab bar while a workout is active — this app's now-playing bar. |
| **Rest clock** | The countdown between sets. |
| **Load state** | A screen's enum for what it has: not loaded, loaded, unavailable with a reason, or stale — a value that is not current. Never flags plus an optional error. |
| **Staleness** | Why a value on screen is not current: a refresh the server answered with a fault, or a server that could not be reached, marked with the time the value dates from ("As of 14:02"). |
| **Remembered read** | A screen's read whose last answer the device keeps for the signed-in account, and shows, marked with its time, when the server cannot be reached. Forgotten at sign-out. |
| **Server reachability** | Whether the server is answering, learned from the app's own requests rather than the phone's network. Its return is what sends waiting work and refreshes screens. |
| **Set command** | A set as the athlete states it — the numbers, and nothing the server owns. Smaller than a set entry, which also carries its source and facets. |
| **Logged set** | A set command in the workout in flight, with the device's own id and the moment it was logged. Neither is sent; they exist so a row can be addressed and a rest recovered. |
| **Set modality** | Which of the four shapes a set is being logged in. A property of the set, not of the movement — the same treadmill is lifted on and run on. |
| **Workout destination** | Where a workout's sets are going to land: an active session already on the server, a deferred start whose session the server has not opened yet, or a `POST /completed` that carries the whole thing at the end. |
| **Deferred start** | A live workout started with no connection. Logged like any other; its session is opened on the server by the first push that reaches it. |
| **Server copy** | What the server already holds of a workout the device sent without hearing back, recognised by the workout's start instant (and, for a completed one, its set count), so a repeated start or delivery never makes a second session. |
| **Finished workout** | A workout the athlete finished that the server has not taken yet, held in the outbox with its end and effort: waiting, or refused with the server's reason. |
| **Outbox** | Where finished workouts wait for the server, one file each, delivered oldest first whenever it answers. |
| **Account custody** | Whose the work held on the device is, kept through a sign-out the server forces so the same athlete signing back in finds it. |
| **Sync state** | Whether what has been logged has reached the server: synced, pending, failed, or refused. Only the last two are worth saying out loud. |
| **Sign-out consequence** | What signing out costs the workout on the device, stated in the sign-out confirmation: nothing, nothing Train cannot resume, the sets not yet synced, a workout the server has never seen, or a whole past workout — plus any finished workouts still waiting. Signing out deletes the workout document and the outbox, so the answer depends on what the server already holds. |
| **Logging surface** | The set-by-set sheet the workout bar opens into. |
| **Profile gate** | The root's answer to whether a signed-in athlete may see the tabs: undetermined, absent, present, or unreachable. An Identity account without a Lionheart profile is refused by every training write, while reads answer empty — which is why the gate asks rather than waiting to be refused. |
| **Session log** | History's list of sessions, grouped by month, newest first — the training log. A completed session reads as a log-book entry: its day and duration, a movement digest per movement, and its total. Other rows read as in progress, skipped, missed or planned — "missed" being a planned session whose day has gone by, which only the client can tell. Filtered by movement by identity, never by name. |
| **Session total** | What a session's sets came to: the set count and the volume (weight × reps) in each unit lifted in. Units never convert. |
| **Movement page** | One movement's screen: its history, its charts, its records, and how often it is trained. Reached from anywhere a movement is named. |
| **Movement history** | Every session one movement was performed in, newest first, with that movement's sets — a filter over the whole log the device already holds. Variants are separate movements. |
| **Movement measure** | What a movement's chart reads per session: the estimated one-rep max, the heaviest set, the best set volume, or the reps at one load. |
| **Movement frequency** | Sessions a week on one movement, over every week since its first session and over the last twelve. |
| **Movement index** | Every movement the log holds, most recently trained first, with its session count. |
| **Consistency reading** | Training days, sessions per week, and the runs of weeks that held a session. Plain measurement — no streak, no target. |
| **Week run** | Consecutive weeks that each held at least one session. The current run survives a week still in progress that has had nothing yet. |
| **Record mark** | The sign on a set that established a record, read from the established records by the session's start instant, the movement and the set as performed. Firsts of their kind and withdrawn records carry none. |
| **Focus reading** | A focus pin read against today and the log: where its horizon stands, sessions against the frequency asked, the estimated one-rep max before against since, the distance to its target, and the last session. |
| **Catalogue coverage** | Making sure the movement catalogue names every movement a screen holds: a missing one has the catalogue read again once; one the server still does not list is not asked about again. |
| **Log span** | How far back a reading of the log reaches: three months, six, a year, or all of it. |
| **Movement catalogue** | Movement names by id, so a set — which carries only a `movementDataId` — can say what it was. |
| **Chat turn** | One thing in the Chat transcript: something said, a question in flight, or a question the server did not keep because the exchange failed. |
| **Contributor reading** | One of Oura's 0–100 score components on a day, against the band the athlete usually sits in — the 28 days before, mean ± one spread. Every component is higher-is-better. |
| **Oura panel** | One collection of an Oura day as a screen shows it — its score, where Oura issues one, and the figures behind it. |
| **Session transition** | A lifecycle move a session row can offer directly — start, skip or delete. Which ones a row offers is read from its state, because the server refuses the others. |
| **Injury action** | What an injury row can offer: resolve, take up again, or delete. Resolving and reopening are one route in two directions, so a row offers exactly one of them. |
| **Action state** | Where a one-shot action the athlete asked for stands: waiting, out, or refused with the server's own words. A refusal is stated, never swallowed. |
| **Confirmation** | What a destructive action asks before it happens — the question, the consequence, and the word on the button, written once and used by every screen that offers the action. |
| **Day selection** | Which day Today shows: today, which follows the clock across midnight, or a fixed past day. Never a future day. |
| **Recovery figure** | One measurement the athlete reads his recovery by — HRV, lowest heart rate, time asleep, breathing from the night's main sleep; readiness, sleep score, temperature, steps from the day — on one day, against his baseline. |
| **Baseline** | The athlete's own normal for a recovery figure: the mean and spread of the 28 days before the day read, the day itself excluded. Fewer than seven readings is not a baseline. |
| **Deviation** | How far a day's figure is from its baseline, in the figure's own unit. Outside one spread in the worse direction, the screen marks it. |
| **Oura coverage** | How far the stored Oura data reaches: the newest day any collection covers, or nothing stored. What a sync is planned from. |
| **Training load** | Sets per week, read against their four-week average. Counts rather than tonnage, because the server derives no session volume and units never convert. |
| **Movement digest** | One movement of a session as a log-book line: the load and every set's reps for straight sets, the top set and the count otherwise. |
