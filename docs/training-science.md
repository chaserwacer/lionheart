# Training science: intensity credit, capacity targets, what's next

The design authority for how the training score credits cardio intensity, how its targets follow
capacity, and how the next lift and the next cardio are recommended. The code it describes:
`Model/Cardio/WorkBouts.cs`, `Model/Cardio/QualityWork.cs`, `Model/Training/Score/Progression.cs`,
`Services/Training/Score/TrainingScoreAnalysis.cs`, `Services/Training/Next/`.

## 1. The problem

The score used to credit any minute at zone 3–5 (70 % of maximum heart rate and above) inside a cardio
session, against 30 a week, and a ring-detected workout counted when its heart rate showed training.
That rule had three flaws:

- **Incidental heart rate earned credit.** A detected dance or game of catch that held zone 2 for half
  an hour was admitted on its heart rate and paid zone-2 minutes. A night out can just as easily hold
  zone 4.
- **Zone 3 was counted as hard.** At 70–80 % of maximum, zone 3 is steady running for a trained
  athlete. Physiologically it sits at or below the first lactate/ventilatory threshold, which in
  trained endurance athletes falls around 77–82 % of maximum heart rate (Seiler 2010). One steady run
  filled the high-intensity target.
- **The target ignored the structure of hard work.** Thirty minutes at any intensity above 70 % does
  not distinguish an interval session from a long run whose heart rate drifted up in the heat.

## 2. What endurance coaches actually count

- **Intensity distribution.** Successful endurance athletes do most of their training at low
  intensity and a minority hard. Counted by sessions, about 80 % are easy and 20 % hard (Seiler and
  Kjerland 2006; Seiler 2010). Two shapes recur: *polarised*, with little work between the
  thresholds, and *pyramidal*, with progressively less at each higher intensity. Elite distance runners
  are mostly pyramidal in preparation and more polarised in competition (Casado et al. 2022), and
  polarised blocks outperformed threshold- or volume-led blocks in trained athletes (Stöggl and
  Sperlich 2014). Either way, the hard work is one to three sessions a week.
- **Count by session goal, not seconds in a zone.** Time-in-zone and session-goal counts of the same
  training differ widely. Elite skiers' time in zone was about 96 % low intensity, while their sessions
  were 87 % easy and 13 % hard (Sylta, Tønnessen and Seiler 2014). Heart rate lags work by one to two
  minutes and drifts upward over a long session, so the coaches' unit is the *session* and its
  *structure*.
- **What a hard session is.** It is either intervals of 2–8 minutes near 90 % of maximum heart rate
  with easy recoveries, or sustained threshold work. The adaptation tracks *accumulated work time* at
  intensity. Four 4-minute bouts at 90–95 % of maximum raised VO2max more than steady running
  (Helgerud et al. 2007). Accumulating 32 minutes at about 90 % (4 × 8) beat 16 minutes at about 95 %
  (4 × 4) in trained cyclists (Seiler et al. 2013). The programming literature describes the same
  menu of sessions: long intervals, short intervals, sprints, and game-based work (Buchheit and Laursen
  2013).
- **Load models.** TRIMP weights time by heart-rate intensity: Banister's exponential form, Edwards'
  zone-weighted sum (already the cardio report's `load`), and Lucia's three-zone version. Session RPE
  multiplies duration by perceived effort (Foster et al. 2001). All of them measure *load*, not
  *stimulus type*. A night of dancing and an interval session can carry the same TRIMP.
- **Incidental heart rate.** Heat, dehydration, caffeine, psychological stress and arousal raise
  heart rate with no matching rise in work (Achten and Jeukendrup 2003). In the heat, cardiovascular
  drift lifts heart rate 10–20 bpm over a steady effort (Coyle and González-Alonso 2001). Heart rate
  alone cannot tell stimulus from circumstance. The tests that separate them are intent (a session
  the athlete chose and recorded), modality (one where he sets and holds the effort), and structure
  (sustained or repeated bouts).

## 3. The rule: quality work

The intensity band is now **quality minutes**, credited only where all three of these hold:

1. **Modality.** The session is endurance: a run, ride, row, swim, ski or machine. A walk or hike gets
   none, because terrain sets its effort. A game or dance gets none, because play sets its effort. An
   activity nobody named gets none (`CardioModality`).
2. **Structure.** The heart rate shows **work bouts**: zone 4 or above (≥ 80 % of maximum, the
   threshold and VO2max range intervals are prescribed in), held for at least **2 minutes**.
   - A dip below zone 4 of 30 seconds or less does not end a bout and is not counted in it.
   - A recovery longer than that ends the bout.
   - Samples more than a minute apart end a bout and stand for nothing, so a sparse background
     series (one reading every few minutes) can never read as intervals. Workout recordings from
     the watch and the ring's workout mode sample every few seconds.
   - Code: `WorkBouts`.
3. **A session's worth.** The bouts add up to at least **10 minutes**: five bouts of 2 minutes, or a
   10-minute threshold block. A single hill surge in an easy run earns nothing.

**What this rule cannot see.** Heart rate drifts up over a long steady effort in the heat (Coyle and
González-Alonso 2001). An easy run whose heart rate climbs past 80 % of maximum and stays there for
ten minutes reads as a threshold block. Telling drift from work needs the work itself: pace or power
against heart rate. The watch's recordings carry speed and power (`WorkoutRecording.Metrics`), so a
decoupling check is the natural next step. Until then, sustained single bouts late in a long session
are the case to read with care in `score-calibration --sessions`.

Separately, **ring-detected games and dances are excluded outright** (`WorkoutExclusion.detectedGame`),
whatever their heart rate: they earn no zone-2 credit and no quality credit. A rate reached in one
still counts toward the observed maximum heart rate, because it is evidence about the heart whatever
the activity was. A game he enters himself,
on the ring or in the log, is still a session and earns zone 2 on its heart rate. Ring-detected
endurance workouts, walks and unnamed workouts are still admitted on their heart rate
(`TrainingEvidence`).

**Zone 2 is unchanged** (60–70 % of maximum, 150 minutes a week committed), as he asked. One open
decision: steady running in zone 3 (70–80 %) now earns neither band. The research would count it as
low-intensity base work for a trained runner. See *Decisions* below.

## 4. Targets that follow capacity

Each volume measurement — hard sets per family, zone-2 minutes, quality minutes — is read against a
`WeeklyTarget`:

| | committed | floor | step |
|---|---|---|---|
| Hard sets per family | 8 | 4 | 1 |
| Zone-2 minutes | 150 | 60 | 5 |
| Quality minutes | 40 | 10 | 5 |

- **Chronic load.** The mean of the four seven-day blocks before the window, over the blocks that
  could be read. Unknown cardio weeks are left out. With none readable, the target is the committed
  standard.
- **Ramp.** This week's target is the chronic figure **+ 10 %**, rounded up to the step and held
  between the floor and the committed standard. That keeps the acute load at about 1.1 times the
  chronic, inside every "safe" band the acute:chronic literature proposes (Gabbett 2016), and well
  under the > 30 % two-week jumps associated with distance injuries in novice runners (Nielsen et al.
  2014). The ratio's predictive value is contested (Impellizzeri et al. 2020), so here it is a ramp,
  not a risk model.
- **The top stays earned.** Each measurement pays **half against this week's target and half
  against the committed standard**, each the square root of the fraction reached and capped at the
  whole (`Progression.Share`). Meeting a comeback target of 4 sets pays 85 % of the family's points;
  only 8 sets pays all of them.
- **The floor** keeps a week after a lay-off from being "met" by almost nothing.

The 40 committed quality minutes are two quality sessions of the 16–32 minutes the trials
accumulated. That matches the one or two hard sessions a week that polarised and pyramidal programmes
hold.

**Consistency** counts sessions trained. A session of nothing but walking, a watch's record of
getting somewhere, is no longer one.

### Week expectations and the adjusted score

Every target above is stated once, in the week's expectations (`WeekExpectationsAnalysis`, pure),
with what was done and a reason: the score, weekly volume, next sessions and the chat all read it,
so no screen's target disagrees with another's.

- **Days available.** The athlete marks days non-training — travel, a busy day — once or every week.
  A session planned on a day speaks for that day, over a weekday marked off every week; a single date
  marked off speaks over a session planned on it.
- **Adjusted target.** Each raw target, its committed figure and its floor, times the share of the
  seven days available, **rounded up** to its step. Rounding up keeps it plain rather than generous:
  one day off rarely lowers anything (three sessions over six days is still three), and a share of a
  session is still a session asked.
- **The weeks before are read as they were.** A week marked off counts as the week it was, so the
  ramp back after travel starts lower, the way tolerance falls. Normalising it away would ask for a
  full week straight after one off.
- **The adjusted score** is the same arithmetic against the adjusted targets — every consistency
  block against its own days — beside the score, never in place of it. It equals the score when
  nothing was marked. A week wholly marked off asks nothing, and its points are out of reach rather
  than given.
- **Weekly volume** reads each family against its adjusted target. Its pattern and muscle targets
  stay the informational four direct sets: no score reads them.

### Calibration

`dotnet run -- score-calibration --user <id> [--weeks <from> <to> | --sessions <from> <to> | --next <day>]`
prints the history's distribution, each week with its measurements against its targets, the cardio
report's sessions and bouts, or the next-session answer. Against the athlete's own history, no week reaches 90, and the best weeks combine full lifting,
some cardio and a new best. A synthetic comeback week that meets every floor (4 sets a family, an hour of zone 2, one
  10-minute quality session, three sessions) scores in the 60s. A committed week with new bests
  scores 100 (`TrainingScoreAnalysisTests`).

Two things the calibration surfaced:

- Weeks from before the ring was worn have no ring heart rate. Watch-recorded sessions now carry
  their own heart rate into the score; before this they made the week unknown.
- A block built on squat, bench and deadlift earns no pull credit, because the deadlift is a hinge,
  which is legs. See *Decisions*.

## 5. What's next

`GET /api/training/next/{day}` (`NextSessionsAnalysis`, pure) answers three things, each with the
reasons it was made from.

**The next lift.** The family trained longest ago, the volume screen's up-next. Each due family's
priority lift gets the week expectations' aim: the last session's best set one step on, which is
+5 lb or +2.5 kg at the same reps, or one more rep where the load is bodyweight, carrying the
twelve-week best estimate a new best has to pass — double progression in its simplest form. It
**holds** last time's set instead when any of these says so (`LiftAimAnalysis`):

- today's going-in rating is under his usual day (under 65) — only today's, since it describes now;
- the last top set was at RPE 9.5 or more, so it has not yet moved easier (autoregulation by RPE,
  Helms et al. 2016);
- he rated the last session of the lift 5 of 5 for difficulty;
- the latest session of the week was rated 4 of 5 or more for accumulated fatigue.

Two outcomes only, so the aim reads at a glance; an easy last session (RPE 8 or under, difficulty 3 or
under) is named as the reason to step. A pinned lift target is stated beside the aim, met or how far,
and the pin's sessions a week is the lift's frequency, prorated like every other expectation.

A day itself marked non-training is said first, so the rest reads as the next training day's, and
the next cardio is sized against the week's adjusted targets.

**The next cardio.**
- A **quality** session when all of these hold: the week's quality target is unmet, none was done in
  the last 2 days, recovery is not low, legs were not trained today, and the week's cardio could be
  read. Hard sessions are spaced 48 hours apart with easy days
  between, the polarised rule (Seiler 2010).
- The structure is the one nearest the minutes still wanted: 5 × 2 min (the least that counts, and the
  start after four weeks without quality work), 4 × 4 min with 3 easy (Helgerud et al. 2007), 3 × 8 or
  4 × 8 min with 2 easy (Seiler et al. 2013).
- Otherwise **zone 2**, for the minutes left to this week's target, between 30 and 75. On a
  low-recovery day it is 30.

**Which first**, in order:
1. **Recovery low → easy cardio.** Low means any of these: Oura's readiness under the tenth percentile
   of its last 90 days; last night's HRV more than one standard deviation under the 28 nights before;
   under 6 hours asleep. HRV-guided training defers intensity on low-HRV days (Kiviniemi et al. 2007;
   Plews et al. 2013). Short sleep impairs performance and recovery (Fullagar et al. 2015).
2. **Hard intervals wait 24–48 h after legs → lift first.** Strength work impairs running economy and
   time to exhaustion the following day (Doma and Deakin 2013). Interference is greatest for running
   and for leg work (Wilson et al. 2012). Legs today also turn the next cardio into zone 2.
3. **Legs due within a day of intervals → easy cardio first, legs next.** If the intervals were
   today, legs are next but tomorrow.
4. **One kind already done today → the other.** This comes after the interference rules, so they
   hold on the same day too.
5. **Otherwise, whichever part of the week's score is further behind.** With the week's cardio
   unread, whichever was done longer ago; and unread cardio never prescribes intervals.

The Train tab and the chat's TODAY briefing (through the `GetNextSessions` tool) read this one
answer, and the workout tracker's header is built to read it too.

## Decisions he may want to change

1. **Zone 3 earns nothing.** Steady running at 70–80 % of maximum is neither zone 2 nor quality. The
   alternative is to count zone 3 minutes in endurance sessions toward the aerobic band, renamed
   zones 2–3. That is closer to the physiology for a trained runner, but it changes the zone 2 he says
   feels right.
2. **Quality floor at zone 4 (80 %).** It could be 85 % to match the trials' near-90 % work more
   tightly. That would credit less tempo work.
3. **Half the points on the ramp, half on the standard.** A larger ramp share rewards a comeback
   more. A smaller one keeps the score closer to the committed standard.
4. **Deadlifts are legs.** A powerlifting block earns no pull credit. Counting the hinge toward pull
   as well would change the volume screen too.
5. **Adjusted targets round up.** Rounding to the nearest would lower more targets for one or two
   days off — kinder, and more of a game.
6. **The weeks before a target keep their days off.** Normalising a week marked off to a full week
   would hold targets up after travel; reading it as it was ramps back from what was done.
7. **The hold rule's thresholds.** Going-in under 65, RPE 9.5, difficulty 5, fatigue 4. Holding at
   RPE 9 or difficulty 4 would hold more often.

## Sources

- Achten J, Jeukendrup AE. Heart rate monitoring: applications and limitations. *Sports Med*
  2003;33(7):517–538.
- Banister EW. Modeling elite athletic performance. In: *Physiological Testing of Elite Athletes*,
  Human Kinetics, 1991.
- Buchheit M, Laursen PB. High-intensity interval training, solutions to the programming puzzle.
  Part I. *Sports Med* 2013;43(5):313–338.
- Casado A, González-Mohíno F, González-Ravé JM, Foster C. Training periodization, methods,
  intensity distribution, and volume in highly trained and elite distance runners: a systematic
  review. *Int J Sports Physiol Perform* 2022;17(6):820–833.
- Coyle EF, González-Alonso J. Cardiovascular drift during prolonged exercise: new perspectives.
  *Exerc Sport Sci Rev* 2001;29(2):88–92.
- Doma K, Deakin GB. The effects of strength training and endurance training order on running
  economy and performance. *Appl Physiol Nutr Metab* 2013;38(6):651–656.
- Edwards S. *The Heart Rate Monitor Book*. Polar, 1993.
- Foster C et al. A new approach to monitoring exercise training. *J Strength Cond Res*
  2001;15(1):109–115.
- Fullagar HHK et al. Sleep and athletic performance: the effects of sleep loss on exercise
  performance, and physiological and cognitive responses to exercise. *Sports Med* 2015;45(2):161–186.
- Gabbett TJ. The training–injury prevention paradox. *Br J Sports Med* 2016;50(5):273–280.
- Helgerud J et al. Aerobic high-intensity intervals improve VO2max more than moderate training.
  *Med Sci Sports Exerc* 2007;39(4):665–671.
- Helms ER, Cronin J, Storey A, Zourdos MC. Application of the repetitions in reserve-based rating
  of perceived exertion scale for resistance training. *Strength Cond J* 2016;38(4):42–49.
- Impellizzeri FM et al. Acute:chronic workload ratio: conceptual issues and fundamental pitfalls.
  *Int J Sports Physiol Perform* 2020;15(6):907–913.
- Kiviniemi AM et al. Endurance training guided individually by daily heart rate variability
  measurements. *Eur J Appl Physiol* 2007;101(6):743–751.
- Lucia A et al. Tour de France versus Vuelta a España: which is harder? *Med Sci Sports Exerc*
  2003;35(5):872–878.
- Nielsen RO et al. Excessive progression in weekly running distance and risk of running-related
  injuries. *J Orthop Sports Phys Ther* 2014;44(10):739–747.
- Plews DJ et al. Training adaptation and heart rate variability in elite endurance athletes.
  *Sports Med* 2013;43(9):773–781.
- Seiler S, Kjerland GØ. Quantifying training intensity distribution in elite endurance athletes.
  *Scand J Med Sci Sports* 2006;16(1):49–56.
- Seiler S. What is best practice for training intensity and duration distribution in endurance
  athletes? *Int J Sports Physiol Perform* 2010;5(3):276–291.
- Seiler S, Jøranson K, Olesen BV, Hetlelid KJ. Adaptations to aerobic interval training:
  interactive effects of exercise intensity and total work duration. *Scand J Med Sci Sports*
  2013;23(1):74–83.
- Stöggl T, Sperlich B. Polarized training has greater impact on key endurance variables than
  threshold, high intensity, or high volume training. *Front Physiol* 2014;5:33.
- Sylta Ø, Tønnessen E, Seiler S. From heart-rate data to training quantification: a comparison of
  3 methods of training-intensity analysis. *Int J Sports Physiol Perform* 2014;9(1):100–107.
- Wilson JM et al. Concurrent training: a meta-analysis examining interference of aerobic and
  resistance exercise. *J Strength Cond Res* 2012;26(8):2293–2307.
