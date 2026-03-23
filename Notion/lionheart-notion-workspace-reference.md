# Lionheart Notion Workspace — Complete Reference

**Created:** March 3, 2026  
**Root Page:** [🦁 Lionheart Fitness](https://www.notion.so/319cfd67ff058104ba19d3d5465fa6dd)

---

## Workspace Structure Overview

```
🦁 Lionheart Fitness (root)
├── 🏠 Home Dashboard
├── 🏋️ Active Workout
├── 📊 Trends & Analysis
├── 📚 My Library
│   ├── 💪 Exercise Library (DB)
│   ├── 🔧 Equipment Library (DB)
│   └── 🔄 Modifier Library (DB)
├── 📋 Daily Check-In (DB)
├── 🏋️ Training Sessions (DB)
├── 📝 Set Log (DB) ⭐ core
├── 📐 Training Programs (DB)
├── 🚶 Activities (DB)
├── 🤕 Injury & Pain Log (DB)
├── 🍎 Nutrition Log (DB)
├── 📊 Custom Metrics (DB)
├── 💭 Recovery & Mental (DB)
├── 🏆 Personal Records (DB) — Lionheart writes
├── 📈 Weekly Analysis (DB) — Lionheart writes
└── 🔮 AI Insights (DB) — Lionheart writes
```

---

## Database IDs & Data Source IDs

Use **Data Source IDs** (collection IDs) for Notion API queries.

| Database | Database ID | Data Source ID |
|---|---|---|
| 💪 Exercise Library | `e8bc91a2-e308-4dab-b376-0534f993819b` | `bce06c5d-ecc4-486d-904c-a39dd0785c5c` |
| 🔧 Equipment Library | `f9452748-7b53-4745-b02b-826b0d975839` | `50acee49-ecae-4bfc-878d-361688c9fd57` |
| 🔄 Modifier Library | `7fec7dde-a041-4bac-ac5d-cab7c2d2fbcd` | `0481e0ff-a308-4ba6-bab5-4a0c4da0341f` |
| 📋 Daily Check-In | `73d9f744-7d55-4221-8734-709fe563fd14` | `4020fd82-1219-46f3-9648-6bae67ff2fc5` |
| 🏋️ Training Sessions | `8cbabc68-13b0-44cb-a52a-4e5b061924e8` | `0e8268d1-a84b-4b56-934a-3aa8e8b69bd8` |
| 📝 Set Log | `e7f36f8a-385c-4861-abc4-88c04238b6bc` | `6a025a6c-3fa3-43fb-9038-cc5e3414dc27` |
| 📐 Training Programs | `102ae01d-3f4c-4539-9b00-5e6d39b47870` | `82b1a08d-3533-4451-baf1-8693d87139d1` |
| 🚶 Activities | `fc1bf1c0-424f-489e-bdc1-01e92447db86` | `79a6173a-fa4a-447e-abab-3a433e0fc844` |
| 🤕 Injury & Pain Log | `e77c2141-6f43-4369-8de5-0680d9dd7ab1` | `83fbc7e6-ce3e-402b-a260-8eebb7a8bdaa` |
| 🍎 Nutrition Log | `7e006a36-fb74-48ce-a410-d450b6ac1b58` | `d5ec9565-ff0d-46f3-8374-33b411b747cc` |
| 📊 Custom Metrics | `15fa4736-653a-43a7-b7f5-8b9693d31138` | `28272437-e5b0-4bfe-82d1-d5647858c0c7` |
| 💭 Recovery & Mental | `160d0026-416a-4aed-aeba-35663e78dd3b` | `7b787700-b01a-468d-a24e-263de0622e08` |
| 🏆 Personal Records | `cd73998f-7eb1-49d3-83cf-af39f956d9b8` | `b887b06a-9d81-4663-8ec4-1326cdaee429` |
| 📈 Weekly Analysis | `3c6bb645-44a4-440d-bebc-5de90ab8c72b` | `bb681fbd-f5f4-4816-9aa8-24137336694e` |
| 🔮 AI Insights | `5745782d-837a-4fae-b865-68e644510ce8` | `8ec99089-a1c9-4f07-bbba-23c41a38efd4` |

---

## Page IDs

| Page | ID |
|---|---|
| 🦁 Lionheart Fitness (root) | `319cfd67-ff05-8104-ba19-d3d5465fa6dd` |
| 📚 My Library | `319cfd67-ff05-8191-9053-c1728d2ed79b` |
| 🏠 Home Dashboard | `319cfd67-ff05-8136-8d7e-c20eeb0ab248` |
| 🏋️ Active Workout | `319cfd67-ff05-81a1-95f9-db142e679519` |
| 📊 Trends & Analysis | `319cfd67-ff05-8103-aa37-c92ef3ab61dc` |

---

## Database Schemas (Property Names for API)

### 💪 Exercise Library
- `ExerciseName` (Title)
- `Description` (Rich Text)
- `PrimaryMuscles` (Multi-select: Chest, Back, Shoulders, Biceps, Triceps, Quads, Hamstrings, Glutes, Calves, Core, Forearms, Traps, Lats, HipFlexors, Adductors, Abductors, FullBody)
- `SecondaryMuscles` (Multi-select — same options)
- `Category` (Select: Compound, Isolation, Cardio, Plyometric, Mobility, Sport-Specific)
- `Notes` (Rich Text)

### 🔧 Equipment Library
- `EquipmentName` (Title)
- `Available` (Checkbox)
- `Notes` (Rich Text)

### 🔄 Modifier Library
- `ModifierName` (Title)
- `Description` (Rich Text)

### 📋 Daily Check-In
- `Date` (Title — date string)
- `DateProp` (Date)
- `Energy` (Select: 1-5)
- `Mood` (Select: 1-5)
- `Stress` (Select: 1-5)
- `Motivation` (Select: 1-5)
- `SleepNotes` (Rich Text)
- `DailyGoals` (Rich Text)
- `EndOfDayReflection` (Rich Text)
- `DayTags` (Multi-select: Rest Day, Travel, Sick, Competition, Deload, High Stress, Poor Sleep, Great Day, PR Day)

### 🏋️ Training Sessions
- `SessionName` (Title)
- `Date` (Date)
- `Program` (Relation → Training Programs) — dual relation, synced as `Sessions`
- `Status` (Select: Planned, Active, Completed, Skipped)
- `SessionNotes` (Rich Text)
- `AccumulatedFatigue` (Select: 1-10)
- `Difficulty` (Select: 1-10)
- `Engagement` (Select: 1-10)
- `ExternalVariables` (Select: 1-10)
- `SetLog` (Relation → Set Log) — auto-created dual relation

### 📝 Set Log ⭐
- `SetLabel` (Title)
- `Session` (Relation → Training Sessions) — dual relation
- `Exercise` (Relation → Exercise Library)
- `Equipment` (Relation → Equipment Library)
- `Modifier` (Relation → Modifier Library)
- `SetType` (Select: Lift, Distance/Time)
- `SetNumber` (Number)
- **Lift fields:**
  - `TargetReps` (Number)
  - `TargetWeight` (Number)
  - `TargetRPE` (Number)
  - `ActualReps` (Number)
  - `ActualWeight` (Number)
  - `ActualRPE` (Select: 5, 5.5, 6, 6.5, 7, 7.5, 8, 8.5, 9, 9.5, 10)
  - `WeightUnit` (Select: lbs, kg)
- **Distance/Time fields:**
  - `TargetDistance` (Number)
  - `ActualDistance` (Number)
  - `DistanceUnit` (Select: m, yd, mi, km)
  - `TargetDuration` (Rich Text — "MM:SS")
  - `ActualDuration` (Rich Text — "MM:SS")
  - `TargetPace` (Rich Text — "MM:SS /unit")
  - `ActualPace` (Rich Text — "MM:SS /unit")
  - `RestDuration` (Rich Text — "MM:SS")
  - `IntervalType` (Select: Continuous, Repetition, Interval)
- **Common:**
  - `Completed` (Checkbox)
  - `Notes` (Rich Text)

> Note: Volume formula (ActualReps × ActualWeight) needs to be added manually in Notion or computed server-side. The Notion API formula syntax had issues during creation.

### 📐 Training Programs
- `ProgramName` (Title)
- `StartDate` (Date)
- `EndDate` (Date)
- `Completed` (Checkbox)
- `Tags` (Multi-select: Hypertrophy, Strength, Powerlifting, Endurance, Cut, Bulk, Maintenance, Peaking, Deload)
- `Sessions` (Relation → Training Sessions) — dual relation
- `Notes` (Rich Text)

### 🚶 Activities
- `ActivityName` (Title)
- `DateTime` (Date with time)
- `DurationMin` (Number)
- `CaloriesBurned` (Number)
- `Summary` (Rich Text)
- `Fatigue` (Select: 1-10)
- `Difficulty` (Select: 1-10)
- `Engagement` (Select: 1-10)
- `Tags` (Multi-select: Outdoor, Indoor, Social, Solo, Recovery, Sport, Work, Errands)

### 🤕 Injury & Pain Log
- `EntryTitle` (Title)
- `DateTime` (Date with time)
- `InjuryName` (Select: Left Knee, Right Knee, Left Shoulder, Right Shoulder, Lower Back, Upper Back, Left Hip, Right Hip, Left Ankle, Right Ankle, Left Wrist, Right Wrist, Neck, Left Elbow, Right Elbow)
- `PainLevel` (Select: 0-10)
- `PainType` (Select: Sharp, Dull, Aching, Burning, Stiffness, Tingling, Throbbing, Numbness)
- `EventType` (Select: Check-in, Treatment, Flare-up, Improvement)
- `IsActive` (Checkbox)
- `Trigger` (Rich Text)
- `WhatHelped` (Rich Text)
- `RelatedSession` (Relation → Training Sessions)
- `RelatedExercise` (Relation → Exercise Library)
- `Notes` (Rich Text)

### 🍎 Nutrition Log
- `Name` (Title) — auto-generated by Notion
- `Date` (Date)
- `Meal` (Select: Breakfast, Lunch, Dinner, Snack, Pre-Workout, Post-Workout)
- `Description` (Rich Text)
- `ProteinEstimate` (Select: Low, Moderate, High, Very High)
- `Hydration` (Select: Poor, Okay, Good, Excellent)
- `Supplements` (Multi-select: Creatine, Protein Shake, Multivitamin, Fish Oil, Caffeine, Electrolytes, Pre-Workout, Other)
- `Notes` (Rich Text)

### 📊 Custom Metrics
- `MetricEntry` (Title)
- `Date` (Date)
- `MetricName` (Select: Body Weight, Blood Pressure, Resting HR, Grip Strength, Vertical Jump, Waist Measurement, Body Fat %, Other)
- `Value` (Number)
- `Unit` (Select: lbs, kg, mmHg, bpm, inches, cm, %, seconds)
- `Category` (Select: Body Composition, Performance, Health, Recovery, Biometric, Custom)
- `Notes` (Rich Text)

### 💭 Recovery & Mental
- `RecoveryEntry` (Title)
- `Date` (Date)
- `MeditationMin` (Number)
- `MobilityStretchingMin` (Number)
- `ColdExposure` (Checkbox)
- `ColdDurationMin` (Number)
- `HeatSauna` (Checkbox)
- `HeatDurationMin` (Number)
- `MentalClarity` (Select: 1-5)
- `AnxietyLevel` (Select: 1-5)
- `GratitudeNote` (Rich Text)
- `RecoveryNotes` (Rich Text)

### 🏆 Personal Records (Lionheart writes)
- `PRTitle` (Title)
- `DateAchieved` (Date)
- `Exercise` (Relation → Exercise Library)
- `PRType` (Select: Strength, Volume)
- `Weight` (Number)
- `Reps` (Number)
- `WeightUnit` (Select: lbs, kg)
- `PreviousPR` (Rich Text)
- `Improvement` (Rich Text)
- `SourceSession` (Relation → Training Sessions)

### 📈 Weekly Analysis (Lionheart writes)
- `Week` (Title)
- `WeekStart` (Date)
- `WeekEnd` (Date)
- `TotalSessions` (Number)
- `TotalVolume` (Number)
- `TotalSets` (Number)
- `AvgSessionRPE` (Number)
- `AvgWellnessScore` (Number)
- `AvgSleepScore` (Number)
- `AvgReadiness` (Number)
- `TrainingLoadTrend` (Select: Increasing, Stable, Decreasing, Deload)
- `InjuryStatus` (Rich Text)
- `AISummary` (Rich Text)
- `Recommendations` (Rich Text)
- `Highlights` (Rich Text)

### 🔮 AI Insights (Lionheart writes)
- `InsightTitle` (Title)
- `DateGenerated` (Date)
- `Category` (Select: Training, Recovery, Nutrition, Injury, Performance, General)
- `Priority` (Select: Info, Suggestion, Warning, Action Required)
- `Content` (Rich Text)
- `RelatedData` (Rich Text)
- `Acknowledged` (Checkbox)

---

## Relation Map

```
Training Programs ←→ Training Sessions (dual: Program / Sessions)
Training Sessions ←→ Set Log (dual: Session / SetLog)
Set Log → Exercise Library (one-way: Exercise)
Set Log → Equipment Library (one-way: Equipment)
Set Log → Modifier Library (one-way: Modifier)
Injury & Pain Log → Training Sessions (one-way: RelatedSession)
Injury & Pain Log → Exercise Library (one-way: RelatedExercise)
Personal Records → Exercise Library (one-way: Exercise)
Personal Records → Training Sessions (one-way: SourceSession)
```

---

## Pre-populated Reference Data

### Exercise Library (19 exercises)
Bench Press, Squat, Deadlift, Overhead Press, Barbell Row, Pull-up, Lat Pulldown, Leg Press, Romanian Deadlift, Lunges, Bicep Curl, Tricep Extension, Lateral Raise, Face Pull, Plank, Running, Cycling, Rowing, Swimming

### Equipment Library (15 items)
Barbell, Dumbbell, Cable Machine, Smith Machine, Kettlebell, Bodyweight, Resistance Band, EZ Bar, Trap Bar, Machine, Pull-up Bar, Dip Station, TRX/Suspension, Cardio Machine, None/Other

### Modifier Library (17 modifiers)
Incline, Decline, Pause, Tempo (3-1-3), Close Grip, Wide Grip, Sumo, Deficit, Banded, Chain, Single Arm, Single Leg, Seated, Standing, Overhead, Behind the Neck, Reverse Grip

---

## Sync Strategy Notes

### Notion → Lionheart (data input)
- Poll databases using `last_edited_time` for incremental sync
- Use Notion page IDs as external reference keys in Lionheart's database
- Set Log is the highest-volume database — optimize polling frequency
- Resolve relation page IDs to Lionheart entities by cross-referencing Exercise/Equipment/Modifier page IDs

### Lionheart → Notion (analysis output)
- Write to Personal Records, Weekly Analysis, and AI Insights databases
- Use `page_id` for deduplication when updating existing entries
- Set `Acknowledged` = false on new AI Insights so users see them as unread

### Property Naming Convention
- PascalCase throughout (e.g., ActualWeight, SetNumber, PainLevel)
- No special characters in property names
- Relations named after target database (Exercise, Equipment, Session, etc.)
- Date properties accessed via expanded format: `date:PropertyName:start`, `date:PropertyName:end`, `date:PropertyName:is_datetime`

---

## TODO / Manual Steps

1. **Add Volume formula to Set Log** — `ActualReps * ActualWeight` formula needs to be added via Notion UI (API formula syntax had parsing issues)
2. **Create database templates** — Notion MCP doesn't support template creation; add these manually:
   - Set Log: "Quick Lift" (SetType=Lift, WeightUnit=lbs, Completed=true), "Quick Lift (kg)", "Programmed Lift", "Cardio Entry"
   - Training Sessions: "Lifting Session" (Status=Active), "Cardio Session", "Planned Workout"
   - Daily Check-In: "Morning Check-In"
   - Nutrition: Templates per meal type
   - Injury: "Quick Pain Log", "Treatment Log"
3. **Configure linked views** — Inside each Training Session page, add a linked view of Set Log filtered by that session's relation
4. **Set up database filters on dashboard** — Configure the Home Dashboard linked views to filter by today's date, active status, etc.
