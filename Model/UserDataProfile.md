# User Health & Wellness Data Profile

This document provides a comprehensive overview of all health and wellness data collected for a user in the Lionheart system, including all types for each field. This profile excludes personally identifiable information.

## Core User Information

### LionheartUser
- `Age`: int
- `Weight`: float

---

## Wellness Data

### WellnessState
Each entry represents the user's wellness state on a specific day.

- `Date`: DateOnly
- `MotivationScore`: int
- `StressScore`: int
- `MoodScore`: int
- `EnergyScore`: int
- `OverallScore`: double

---

## Oura Ring Data

### DailyOuraInfo
Complete daily Oura data snapshot for a specific date.

**Base Properties:**
- `Date`: DateOnly
- `SyncDate`: DateOnly

### OuraRingSleepData (SleepData)
- `SleepScore`: int
- `DeepSleep`: int - Contribution score for deep sleep
- `Efficiency`: int - Sleep efficiency contribution score
- `Latency`: int - Sleep latency (time to fall asleep) contribution score
- `RemSleep`: int - REM sleep contribution score
- `Restfulness`: int - Restfulness contribution score
- `Timing`: int - Sleep timing contribution score
- `TotalSleep`: int - Total sleep contribution score

### OuraRingActivityData (ActivityData)
- `ActivityScore`: int - Overall activity score
- `Steps`: int - Total steps
- `ActiveCalories`: int - Active calories burned
- `TotalCalories`: int - Total calories burned
- `TargetCalories`: int - Target calorie goal
- `MeetDailyTargets`: int - Contribution score for meeting daily targets
- `MoveEveryHour`: int - Contribution score for moving every hour
- `RecoveryTime`: int - Contribution score for recovery time
- `StayActive`: int - Contribution score for staying active
- `TrainingFrequency`: int - Contribution score for training frequency
- `TrainingVolume`: int - Contribution score for training volume

### OuraRingResilienceData (ResilienceData)
- `SleepRecovery`: double - Sleep recovery score
- `DaytimeRecovery`: double - Daytime recovery score
- `Stress`: double - Stress level
- `ResilienceLevel`: string - Overall resilience level (e.g., "normal", "high")

### OuraRingReadinessData (ReadinessData)
- `ReadinessScore`: int - Overall readiness score
- `TemperatureDeviation`: double - Body temperature deviation from baseline
- `ActivityBalance`: int - Contribution score for activity balance
- `BodyTemperature`: int - Body temperature contribution score
- `HrvBalance`: int - Heart rate variability balance contribution score
- `PreviousDayActivity`: int - Previous day activity contribution score
- `PreviousNight`: int - Previous night's sleep contribution score
- `RecoveryIndex`: int - Recovery index contribution score
- `RestingHeartRate`: int - Resting heart rate contribution score
- `SleepBalance`: int - Sleep balance contribution score

---

## Activity Tracking

### Activity
Base activity data for any physical activity.

**Base Properties:**
- `DateTime`: DateTime
- `TimeInMinutes`: int
- `CaloriesBurned`: int
- `Name`: string
- `UserSummary`: string

**Feel Data:**
- `AccumulatedFatigue`: int
- `DifficultyRating`: int
- `EngagementRating`: int
- `ExternalVariablesRating`: int

### RunWalkDetails (Optional)
Sport-specific data for running/walking activities.

- `Distance`: double
- `ElevationGain`: int
- `AveragePaceInSeconds`: int
- `MileSplitsInSeconds`: List<int>
- `RunType`: string (e.g., "Zone 2 Rail Trail", "Exploration Hike")

### LiftDetails (Optional)
Sport-specific data for lifting activities.

- `Tonnage`: int
- `LiftType`: string (e.g., "PL", "BodyBuilding")
- `LiftFocus`: string (e.g., "Legs", "Squat + Bench")
- `QuadSets`: int
- `HamstringSets`: int
- `BicepSets`: int
- `TricepSets`: int
- `ShoulderSets`: int
- `ChestSets`: int
- `BackSets`: int

### RideDetails (Optional)
Sport-specific data for cycling activities.

- `Distance`: double
- `ElevationGain`: int
- `AveragePower`: int
- `AverageSpeed`: double
- `RideType`: string (e.g., "Mtb Trail Ride")

---

## Training Programs

### TrainingProgram
Structured training program with sessions.

- `Title`: string
- `StartDate`: DateOnly
- `NextTrainingSessionDate`: DateOnly
- `EndDate`: DateOnly
- `IsCompleted`: bool
- `TrainingSessions`: List<TrainingSession>
- `Tags`: List<string>

### TrainingSession
Individual training session within a program.

- `Date`: DateOnly
- `Status`: TrainingSessionStatus (enum: Planned, InProgress, Completed, Skipped, AIModified)
- `Movements`: List<Movement>
- `CreationTime`: DateTime
- `Notes`: string

### Movement
Specific movement/exercise within a training session.

- `MovementBase`: MovementBase
- `MovementModifier`: MovementModifier
- `LiftSets`: List<LiftSetEntry>
- `DistanceTimeSets`: List<DTSetEntry>
- `Notes`: string
- `IsCompleted`: bool
- `Ordering`: int

### MovementBase
Base movement template/definition.

- `Name`: string
- `Description`: string
- `MuscleGroups`: List<MuscleGroup> (enum: Chest, Back, Hamstrings, Quadriceps, SideDeltoids, FrontDeltoids, RearDeltoids, Biceps, Triceps, Calves, Abs, Glutes, Forearms, Traps, Lats, LowerBack, Neck)

### MovementModifier
Modification details for how to perform a movement.

- `Name`: string
- `Equipment`: Equipment

### Equipment
Equipment used in training.

- `Name`: string

### LiftSetEntry
Individual set entry for lifting movements.

- `RecommendedReps`: int
- `RecommendedWeight`: double
- `RecommendedRPE`: double
- `ActualReps`: int
- `ActualWeight`: double
- `ActualRPE`: double
- `WeightUnit`: WeightUnit (enum: Kilograms, Pounds)

### DTSetEntry (Distance/Time Set Entry)
Individual set entry for cardio/endurance movements.

- `RecommendedDistance`: double
- `ActualDistance`: double
- `IntervalDuration`: TimeSpan
- `TargetPace`: TimeSpan
- `ActualPace`: TimeSpan
- `RecommendedDuration`: TimeSpan
- `ActualDuration`: TimeSpan
- `RecommendedRest`: TimeSpan
- `ActualRest`: TimeSpan
- `IntervalType`: IntervalType (enum: ContinuousDistance, ContinuousTime, ContinuousDistanceAndTime, RepetitionDistance, RepetitionTime, RepetitionDistanceAndTime, IntervalDistance, IntervalTime, IntervalDistanceAndTime)
- `DistanceUnit`: DistanceUnit (enum: Meters, Yards, Miles, Kilometers)
- `ActualRPE`: double

---

## Injury Tracking

### Injury
Injury record with event history.

- `Name`: string
- `Notes`: string
- `InjuryDate`: DateOnly
- `IsActive`: bool
- `InjuryEvents`: List<InjuryEvent>

### InjuryEvent
Individual injury event (treatment or check-in).

- `Notes`: string
- `PainLevel`: int
- `InjuryType`: InjuryEventType (enum: treatment, checkin)
- `CreationTime`: DateTime

---

## Summary Statistics

### Total Data Points Per User

**User Core**: 2 fields (Age, Weight)

**Wellness States**: 6 fields per entry × N entries

**Oura Daily Data**: 38 fields per day × N days
- Sleep: 8 fields
- Activity: 11 fields
- Resilience: 4 fields
- Readiness: 10 fields
- Metadata: 2 fields

**Activities**: 8-18 fields per activity × N activities
- Base: 7 fields
- RunWalk: +5 fields
- Lift: +7 fields
- Ride: +5 fields

**Training Programs**: Variable structure
- Program: 7 fields per program
- Sessions: 5 fields per session
- Movements: 7+ fields per movement
- Sets: 7-13 fields per set (depending on type)

**Injuries**: 5 fields per injury + N events × 4 fields

---

## Data Type Summary

- **string**: Text fields (names, descriptions, notes, activity types)
- **int**: Scores, counts, durations in minutes/seconds, reps
- **double**: Decimal measurements (weight, distance, RPE, recovery scores, pace)
- **float**: User weight
- **bool**: Status flags (IsCompleted, IsActive)
- **DateOnly**: Calendar dates
- **DateTime**: Timestamps with time component
- **TimeSpan**: Durations and paces
- **Enums**: Type-safe categorical values (TrainingSessionStatus, MuscleGroup, WeightUnit, InjuryEventType, IntervalType, DistanceUnit)
- **List<T>**: Collections of related health/wellness data
