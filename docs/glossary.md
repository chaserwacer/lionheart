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
| **Coached programme sheets** | The athlete's 2023–24 powerlifting spreadsheets (Jamal V5; Blake Rossi's Perfect Powerlifting), read through training-os's derived set log. Each sheet imports as a completed training program. |
| **Not-performed row** | A row the set log holds that the athlete did not lift as written — a weight the spreadsheet computed, a placeholder, a row his own note contradicts. Left out of the session and named in its notes. |
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
| **Record rebuild** | Re-detecting an athlete's records from scratch over every completed session, oldest first — what keeps the chains right when history arrives out of order. Writes nothing when the records are already right. |
| **Revising a session** | Correcting a completed session after the fact: its sets, when it happened and its notes, restated whole. It stays completed. A change to the sets or the start rebuilds the records. |
| **Retracting a record** | Withdrawing the standing record because the set behind it was not real, which puts back whatever it beat. The only edit an athlete makes to a record. |
| **Set kind** | What a lift was for: working, warm-up, drop or failure. A warm-up sets no record and counts toward no total or target; drop and failure sets count like working sets. |
| **Working set** | A set that takes a place in the plan: a working or failure set, the nth of its movement, read against the nth target, a routine's count and last time's nth working set. A warm-up prepares and a drop set extends the set before it, so neither takes a place; a drop set still counts toward totals and records. |
| **Routine** | A session trained again and again, saved: a name and an order of movements with a working-set count each, and no weights. A workout starts from one in a tap. |
| **Routine origin** | Whether a session was trained from a routine, and which: ad hoc, or from a routine. Kept when the routine is deleted. |
| **Previous performance** | A movement's sets from the most recent completed session holding it — "last time". |
| **Movement setup** | The athlete's notes on a movement, and whether its logged weight is plate added to bodyweight. |
| **Estimated one-rep max** | A single rep implied by a set, by Epley. Derived from the stored weight and reps on the way out, never stored. |
| **Established records** | Every record a movement or a window of time set, whatever became of each since — standing, beaten or withdrawn. What marks the sets that set a record. |
| **Focus pin** | A movement the athlete has pinned to emphasise over a horizon, at a number of sessions a week, perhaps toward a target. Pinned or unpinned; an unpinned pin is kept as history. |
| **Focus terms** | What a focus pin asks for: its horizon, its sessions a week and its target. Revised whole, and only while the pin is pinned. |
| **Focus horizon** | The days a focus pin covers, first and last included. Whether it has begun or passed is read against today, never stored. |
| **Focus target** | No target, or a lift target — a weight in its unit for a rep count. Whether it was met is read against the training log, never stored. |
| **Unpinning** | Letting a focus go, on a day the athlete names. Once only; delete is for a pin made by mistake. |
| **Focus role** | What a pin stands for: the priority lift of push, pull or legs, or a movement emphasised on its own. Fixed for the pin's life. |
| **Priority lift** | The one lift a family is built around for a training block — weighted dips for push, hack squat for legs, pull-ups for pull. A pinned priority; pinning a new one lets the last go. With none pinned, the family's most-trained compound lift of eight weeks stands in. |
| **Movement pattern** | The shape of a movement's work: horizontal or vertical push or pull, squat, hinge, single-leg, isolation, or other. |
| **Pattern family** | Push, pull or legs — the three groups the athlete judges gym health by — and core for work in none of them. A compound pattern names its own; accessory work takes its first primary muscle's. |
| **Muscle group** | One of sixteen groups weekly volume is counted against: chest, front, side and rear delts, triceps, lats, upper back, biceps, forearms, quads, hamstrings, glutes, adductors, calves, lower back, abs. |
| **Movement classification** | What a movement base trains: its pattern, its primary muscle groups and its secondary ones. Stated by the athlete, else read from the movement-pattern map, else unclassified. |
| **Movement-pattern map** | The athlete-editable file, `movement-patterns.json`, saying what each movement base trains, with judgement calls marked unsure. Read live, so a correction reaches every base the athlete has not classified himself. |
| **Hard set** | A working, drop or failure set of a lift; never a warm-up. The unit weekly volume is counted in: whole toward each primary muscle, half toward each secondary one. |
| **Weekly volume** | Hard sets over the seven days ending on a day, by family, pattern and muscle group, each against a weekly target, with the last day each was trained. |
| **Training balance** | Where the week's training stands across the body: weekly volume and up next, the priorities, and the training score. The name of the folders, store and routes that read them. |
| **Up next** | The family or families trained longest ago, named together when last trained the same day, with the day's readiness beside it where it is unusual. A recommendation, never an instruction. |
| **Training score** | Lionheart's own 0–100 for the seven days ending on a day: lifting volume, cardio, priority progress and consistency, each read as measurements against targets. Not Oura's activity score. A part with no data is unknown: named, and its points out of reach rather than counted as zero. |
| **Session source** | Where a session came from: the athlete's log — entered in Lionheart, or imported from his own logs — or Apple Health, with the workout's Health id and the app that recorded it. Orthogonal to provenance. |
| **Health workout** | A workout another app wrote to Apple Health, read by the phone and offered to the server, which decides whether it becomes training. |
| **Workout recording** | What the app that recorded an imported run captured beyond the session: its route and its own heart rate. Kept beside the session, never in it. |
| **Recorded run** | A run imported from Apple Health read for its page: the session's figures, the route's moving time, descent and splits, and the heart rate labelled with whose it is. |
| **Split** | One kilometre or mile of a recorded run, or what is left after the last whole one: its time, moving pace, climb, descent and average heart rate. |
| **Moving time** | A route's time in motion: its fix-to-fix legs faster than a slow shuffle. Stops, and a paused recording, are left out. |
| **Heart-rate source** | Whose samples a session's heart rate was read from: the ring's all-day series, or the recording app's own, through Apple Health. |
| **Health sync** | Offering the runs Apple Health gained since the kept anchor to the server, a page at a time; the anchor moves on once a page is answered. |
| **Oura's score** | A 0–100 rating Oura gives a day or one of its contributors — readiness, recovery index, training frequency. A judgement Oura made, not a measurement; shown as Oura's and never mixed with Lionheart's figures. |

## Body and context

| Word | What it means |
|---|---|
| **Context** | Anything outside a training session that bears on performance — a night's sleep, a stressful week, a day of pressure-washing. |
| **Exertion context** | Physical effort outside training. Carries effort ratings. |
| **State context** | A body or life state. Its valence lives in its decoration. |
| **Wellness state** | The athlete's own rating of a day: energy, mood, motivation and stress, each 1–5, and the server's overall mean of them with stress counted the other way round. One per day; logging the day again replaces it. |
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
| **Complete day** | An activity day whose timeline covers at least 23 hours, with no more than three of them off the finger or lying still. Only complete days count toward a habits summary; today never does. |
| **Habits report** | How the athlete's days are shaped over a span — sittings, movement and training by hour, steps, sleep timing — split into weekdays and weekends and set against the equal span before. |
| **Heart-rate series** | Oura's all-day heart rate: samples about every five minutes, faster in workouts, each with what the ring was doing. Stored as one run per UTC day. |
| **Cardio session** | A logged run — imported from Apple Health or not — or a ring workout that is endurance, a sport, or a walk of 30 minutes or more, and is not inside a logged session. |
| **Maximum heart rate** | What the zones are drawn from: the higher of the age prediction and the ring's own cardio peak (its 99th percentile over a year). |
| **Heart-rate zones** | Five bands at 50, 60, 70, 80 and 90 per cent of maximum heart rate. Zone 2 is 60–70 per cent. |
| **Cardio load** | Edwards' training impulse: minutes in each zone times the zone's number, summed. |
| **Aerobic base** | Whether cardio fitness is moving: zone-2 and cardio minutes per week, VO2max, the nights' lowest heart rate and cardiovascular age, over the last four weeks against the eight before. |
| **VO2max estimate** | Oura's estimate of maximal oxygen uptake from heart rate and walking. An estimate, not a test; read for its trend. |
| **Cardiovascular age** | Oura's predicted vascular age. A model output; read for its trend. |
| **Readiness** | Oura's own composite score for how recovered the body is. Not Lionheart's judgement. |
| **Oura score** | Any of Oura's 0–100 ratings — readiness, sleep, activity and every contributor behind them. How Oura rated something against the athlete's baseline, never a measurement: a resting-heart-rate score of 80 is not 80 bpm. `OuraScore` in the client. |
| **Readiness contributor** | One of the nine Oura scores readiness is built from. Today shows six of them, each beside the measurement it rests on: HRV, the night's lowest heart rate and when it came, the fortnight's sleep, the night's sleep, temperature. |
| **Diary day** | One day as History's day page reads it: Oura's scores, the night's measurements, steps, every workout — lifting from the log, the ring's workouts from the cardio report — and the athlete's wellness rating. |
| **Diary week** | One Monday-to-Sunday week: its lifting by session (working sets, volume per unit), its cardio minutes by zone, and the ring's week in means. |
| **Bodyweight measurement** | The athlete's bodyweight on one calendar day, logged or measured by a scan. One per day; logging the day again replaces it. |
| **Body-composition scan** | One analyser result sheet (InBody): the day and minute it printed, and the sheet. Recording one also logs its weight as that day's bodyweight measurement unless the athlete keeps the day's own. |
| **Sheet** | What one scan printed: the core figures every analyser prints (weight, skeletal muscle mass, body fat mass, lean body mass, percent body fat, BMI, height) and the panels this analyser carried. |
| **Panel** | One block of a sheet that depends on the analyser — body water, dry lean mass, segmental lean, segmental fat, ECW/TBW, fitness score, fat and lean control, basal metabolic rate, visceral fat level, impedance. Absent when not printed or not legible. (A *section* in the code.) |
| **Percent of reference** | A segment's mass as a percentage of the analyser's own reference for the athlete's height and weight. The device's band, not a clinical norm. |
| **Segmental balance** | Left against right for the arms and legs, and the arm–trunk–leg pattern, from the segmental lean panel. |
| **System load** | Bodyweight plus the plate, for a movement whose weight is added to bodyweight — what a weighted dip is judged by. |
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
| **Thread** | The athlete's one continuing chat with Lionheart, read back as a single transcript. Beside it, a **conversation** is a standalone chat he starts and names; nothing said in one is remembered. |
| **Thread session** | The run of thread messages the model actually reads: it ends after three hours idle or once its transcript passes about 60k tokens, and its system prompt is frozen when it opens. |
| **Memory** | What the athlete told the thread, distilled when a session ends: a dated goal, injury, preference, commitment, decision or fact, traced to the messages it came from. The context card is what his data shows; memory is what he said. |
| **Consolidation** | Distilling an ended thread session into memory: one structured model call that remembers, revises or retires. |
| **Supersede** | How a memory changes: a revision writes a new current version and keeps the old one, dated, as its history. |
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
| **Last time** | The entry row's and a movement section's reading of a previous performance: the working set at the same position, and the whole of it in one line. |
| **Set guide** | The entry row's one line above the fields: last time at this position when it differs, the system load, and the plates. |
| **Plate loading** | The plates a side for a barbell weight, heaviest first, on a 45 lb bar, and how far under the weight they fall when they cannot make it. |
| **Warm-up ramp** | The warm-up sets offered before a working weight, each rounded down to what the equipment makes. Offered, never inserted. |
| **Finish readout** | What a finished workout came to, built before it is handed over: duration, working sets, volume, records, the last session of its routine, and each movement's best set against last time. |
| **Movement digest** | One movement of a session as a log-book line: the load and every set's reps for straight sets, the top set and the count otherwise. |
