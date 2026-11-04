import Foundation

/// Turning domain values into the strings a screen shows.
///
/// It is in `Support/` and not `Core/` because it is presentation: the decisions behind these
/// strings — which day counts as "yesterday", whether a readiness is worth showing — are already
/// made in `Core/Derived/`, and this only chooses words for them.
enum Formatting {
    /// Elapsed time as a clock. Hours appear only once there are any, so a 40-minute session reads
    /// `40:12` rather than `00:40:12`.
    static func clock(seconds: TimeInterval) -> String {
        let whole = Int(max(0, seconds))
        let (hours, minutes, remainder) = (whole / 3600, (whole % 3600) / 60, whole % 60)
        return hours > 0
            ? String(format: "%d:%02d:%02d", hours, minutes, remainder)
            : String(format: "%d:%02d", minutes, remainder)
    }

    /// A session's duration in words — "48 min", "1 h 12 min".
    static func duration(seconds: TimeInterval) -> String {
        let minutes = Int((max(0, seconds) / 60).rounded())
        guard minutes >= 60 else { return "\(minutes) min" }
        return minutes % 60 == 0 ? "\(minutes / 60) h" : "\(minutes / 60) h \(minutes % 60) min"
    }

    /// How a day reads next to today.
    static func day(_ relative: RelativeDay, calendar: Calendar = .current) -> String {
        switch relative {
        case .today: "Today"
        case .yesterday: "Yesterday"
        case .tomorrow: "Tomorrow"
        case .daysAgo(let days): "\(days) days ago"
        case .inDays(let days): "In \(days) days"
        case .distant(let day): date(day, calendar: calendar)
        }
    }

    static func date(_ day: CalendarDay, calendar: Calendar = .current) -> String {
        day.middayDate(in: calendar).formatted(.dateTime.weekday(.abbreviated).month(.abbreviated).day())
    }

    /// A day with its year when it is not this year's — for lists that run back across years,
    /// where "Sun, Sep 20" alone could be either.
    static func date(_ day: CalendarDay, today: CalendarDay, calendar: Calendar = .current) -> String {
        guard day.year != today.year else { return date(day, calendar: calendar) }
        return day.middayDate(in: calendar)
            .formatted(.dateTime.weekday(.abbreviated).month(.abbreviated).day().year())
    }

    /// A Monday-to-Sunday week by its two ends — "27 Oct – 2 Nov".
    static func week(startingOn start: CalendarDay, today: CalendarDay = CalendarDay(Date()), calendar: Calendar = .current) -> String {
        span(from: start, to: start.adding(days: 6, in: calendar), today: today, calendar: calendar)
    }

    /// A run of days by its two ends — "3 Mar – 27 Apr" — with the year where it is not this one,
    /// said once when both ends share it.
    static func span(
        from first: CalendarDay, to last: CalendarDay, today: CalendarDay = CalendarDay(Date()), calendar: Calendar = .current
    ) -> String {
        let day = Date.FormatStyle.dateTime.day().month(.abbreviated)
        let end = last.year == today.year ? last.middayDate(in: calendar).formatted(day)
            : last.middayDate(in: calendar).formatted(day.year())
        let start = first.year == last.year ? first.middayDate(in: calendar).formatted(day)
            : first.middayDate(in: calendar).formatted(day.year())
        return "\(start) – \(end)"
    }

    /// What a span of days covers, said under the control that picks it: "Last 90 days: 6 Aug –
    /// 3 Nov".
    static func spanCaption(_ span: TrendSpan, endingOn today: CalendarDay, calendar: Calendar = .current) -> String {
        "Last \(span.days) days: \(Formatting.span(from: today.adding(days: 1 - span.days, in: calendar), to: today, today: today, calendar: calendar))"
    }

    /// A month as a section header — "October 2026".
    static func month(_ day: CalendarDay, calendar: Calendar = .current) -> String {
        day.middayDate(in: calendar).formatted(.dateTime.month(.wide).year())
    }

    /// When a remembered answer dates from, as an instrument would mark it: "As of 14:02" today,
    /// "As of yesterday 18:40", and the date for anything older, so an old reading cannot pass for
    /// a recent one at a glance.
    static func asOf(_ instant: Date, now: Date = Date(), calendar: Calendar = .current) -> String {
        let time = instant.formatted(date: .omitted, time: .shortened)
        switch RelativeDay.of(CalendarDay(instant, in: calendar), today: CalendarDay(now, in: calendar), calendar: calendar) {
        case .today:
            return "As of \(time)"
        case .yesterday:
            return "As of yesterday \(time)"
        case .tomorrow, .daysAgo, .inDays, .distant:
            return "As of \(instant.formatted(.dateTime.month(.abbreviated).day())) \(time)"
        }
    }


    /// What a session log row says under its headline, or `nil` where the state has nothing to
    /// count. The facts come from `Core/`; this only chooses the words and the separator.
    static func sessionFacts(_ facts: LoggedSessionFacts) -> String? {
        switch facts {
        case .performed(let setCount, let duration, let hasEffort):
            var parts = [count(setCount, "set")]
            if let duration { parts.append(self.duration(seconds: duration)) }
            if hasEffort { parts.append("effort rated") }
            return parts.joined(separator: " · ")
        case .inProgress(let setCount):
            return "\(count(setCount, "set")) so far"
        case .prescribed(let prescriptions):
            return prescriptions == 0 ? "No prescriptions" : count(prescriptions, "prescription")
        case .none:
            return nil
        }
    }


    /// A facet's axis in the athlete's words. The axes are a fixed set, so these are a fixed list;
    /// an axis this build has not heard of is named by its wire value rather than hidden, because
    /// the facet's own value is still worth reading.
    static func facetAxis(_ axis: FacetAxis) -> String {
        switch axis {
        case .modality: "Modality"
        case .movementPattern: "Pattern"
        case .muscleRegion: "Region"
        case .intent: "Intent"
        case .lifeDomain: "Life domain"
        case .bodyState: "Body state"
        case .unrecognised(let wire): "Axis \(wire)"
        }
    }

    /// A count with its noun, pluralised. The noun is passed in singular.
    static func count(_ value: Int, _ singular: String, plural: String? = nil) -> String {
        value == 1 ? "1 \(singular)" : "\(value) \(plural ?? singular + "s")"
    }

    /// What a plain-words entry the server committed came to — the sheet's line, and Siri's.
    static func logged(sessions: Int, contexts: Int) -> String {
        switch (sessions, contexts) {
        case (0, 0): "Logged."
        case (let sessions, 0): "Logged \(count(sessions, "session"))."
        case (0, let contexts): "Logged \(count(contexts, "context"))."
        case (let sessions, let contexts):
            "Logged \(count(sessions, "session")) and \(count(contexts, "context"))."
        }
    }

    // MARK: - Sets

    /// A measurement with no trailing zeros: `100`, `102.5`, `2.25`.
    ///
    /// Plate maths gives halves and quarters and nothing finer, so two decimals is the whole range,
    /// and a weight that happens to be round should read round.
    static func measure(_ value: Double) -> String {
        value.formatted(.number.precision(.fractionLength(0...2)).grouping(.never))
    }

    /// A weight in pounds, whichever unit it was logged in: `225 lb`.
    static func weight(_ value: Double, _ unit: WeightUnit) -> String {
        let parts = weightParts(value, unit)
        return parts.unit.isEmpty ? parts.amount : "\(parts.amount) \(parts.unit)"
    }

    /// A weight's figure and unit apart, for a row that aligns them in columns. A weight logged in
    /// kilograms converts to the tenth of a pound — finer than any plate, and no finer, so 100 kg
    /// reads `220.5` rather than `220.46`. A unit this build cannot name keeps its number and says
    /// no unit, rather than calling it pounds.
    static func weightParts(_ value: Double, _ unit: WeightUnit) -> (amount: String, unit: String) {
        switch unit {
        case .pounds: (measure(value), "lb")
        case .kilograms: (measure((Imperial.pounds(value, from: unit) * 10).rounded() / 10), "lb")
        case .unrecognised: (measure(value), "")
        }
    }

    /// A distance in the unit it reads in: kilometres as miles, a track's metres and yards as
    /// logged (`Imperial.distance`).
    static func distance(_ value: Double, _ unit: DistanceUnit) -> String {
        let parts = distanceParts(value, unit)
        return parts.unit.isEmpty ? parts.amount : "\(parts.amount) \(parts.unit)"
    }

    static func distanceParts(_ value: Double, _ unit: DistanceUnit) -> (amount: String, unit: String) {
        let shown = Imperial.distance(value, unit)
        return (measure(shown.value), shown.unit.abbreviation)
    }

    /// A trail run's or a ride's climb, signed: `+459 ft`.
    static func climb(meters: Double) -> String {
        "\(meters < 0 ? "−" : "+")\(Imperial.climb(meters: abs(meters)))"
    }

    /// What a set did, in one line. `nil` for a set whose words are the record — a freeform set's
    /// text is the row, not a figure beside one.
    static func set(_ command: SetCommand) -> String? {
        switch command {
        case .lift(let set):
            return "\(weight(set.weight, set.unit)) × \(set.reps)"
        case .distanceTime(let set):
            return "\(distance(set.distance, set.unit)) in \(clock(seconds: set.duration))"
        case .trailRun(let set):
            return "\(distance(set.distance, set.unit)) in \(clock(seconds: set.duration))"
                + " · \(Imperial.climb(meters: set.elevationGainMeters)) gain"
        case .endurance(let set):
            return "\(set.activity.title) \(distance(set.distance, set.unit)) in \(clock(seconds: set.duration))"
                + " · \(Imperial.climb(meters: set.elevationGainMeters)) gain"
        case .freeform:
            return nil
        }
    }

    /// What a set was asked to do.
    static func target(_ prescription: SetPrescription) -> String {
        switch prescription {
        case .lift(let target):
            let numbers = "\(weight(target.targetWeight, target.unit)) × \(target.targetReps)"
            guard let rpe = target.targetRpe.value else { return numbers }
            return "\(numbers) @ \(self.rpe(rpe))"
        case .distanceTime(let target):
            return "\(distance(target.targetDistance, target.unit))"
                + " in \(clock(seconds: target.targetDuration))"
        case .freeform(let text):
            return text
        case .unrecognised:
            // A target this build cannot read still occupies a slot in the plan, so it says so
            // rather than vanishing and making the session look shorter than it is.
            return "A target this version cannot show"
        }
    }

    /// An RPE as the athlete writes it: `8`, `7.5`.
    static func rpe(_ value: Double) -> String {
        "RPE \(measure(value))"
    }

    /// A running volume total — `4,250 lb`, with the grouping a four-figure number wants.
    static func volume(_ total: WorkoutTotals.WeightTotal) -> String {
        volume(total.volume, total.unit)
    }

    /// A volume in pounds, whichever unit it was lifted in, to a tenth at most.
    static func volume(_ volume: Double, _ unit: WeightUnit) -> String {
        let figure = Imperial.pounds(volume, from: unit).formatted(.number.precision(.fractionLength(0...1)))
        switch unit {
        case .pounds, .kilograms: return "\(figure) lb"
        case .unrecognised: return figure
        }
    }

    /// What a session's sets came to — "14 sets · 12,450 lb" — as one volume in pounds, a part
    /// lifted in kilograms converted into it. Lifts that moved no load (bodyweight work recorded at
    /// zero) add no volume to say; a unit this build cannot name is said on its own, unconverted.
    static func totals(_ totals: WorkoutTotals) -> String {
        let loaded = totals.weights.filter { $0.volume > 0 }
        let named = loaded.filter { $0.unit == .pounds || $0.unit == .kilograms }
        let pounds = named.isEmpty ? [] : [volume(named.map { Imperial.pounds($0.volume, from: $0.unit) }.reduce(0, +), .pounds)]
        let unnamed = loaded.filter { $0.unit != .pounds && $0.unit != .kilograms }.map(volume)
        return ([count(totals.setCount, "set")] + pounds + unnamed).joined(separator: " · ")
    }

    /// How much of a session set a record: the sets that did, and the movements whose best
    /// session volume it set.
    static func recordSummary(sets: Int, sessionVolumes: Int) -> String {
        let setPart = sets > 0 ? [count(sets, "set")] : []
        let volumePart = sessionVolumes > 0
            ? ["session volume on \(count(sessionVolumes, "movement"))"] : []
        return (setPart + volumePart).joined(separator: " · ")
    }

    /// Sessions a week to two places, the way the athlete's own analysis states it: `0.19`.
    static func rate(_ rate: Double) -> String {
        rate.formatted(.number.precision(.fractionLength(2)))
    }

    // MARK: - Recovery

    /// A recovery figure as a number and the unit beside it, apart so the number can carry the
    /// weight and the unit sit quieter.
    static func figure(_ value: Double, of metric: RecoveryMetric) -> (number: String, unit: String?) {
        switch metric {
        case .readiness, .sleepScore: (whole(value), nil)
        case .hrv: (whole(value), "ms")
        case .lowestHeartRate: (whole(value), "bpm")
        case .respiratoryRate: (value.formatted(.number.precision(.fractionLength(1))), "/min")
        case .temperatureDeviation: (signed(value, fractionDigits: 1), "°F")
        case .steps: (value.formatted(.number.precision(.fractionLength(0))), nil)
        case .sleepDuration: (hoursAndMinutes(seconds: value), nil)
        }
    }

    /// A figure as one string, for VoiceOver and the trend summary.
    static func figureText(_ value: Double, of metric: RecoveryMetric) -> String {
        let parts = figure(value, of: metric)
        return parts.unit.map { "\(parts.number) \($0)" } ?? parts.number
    }

    /// How far a figure is from the athlete's baseline, signed, in the figure's own unit: `+8`,
    /// `−2`, `−0:24`. A difference that rounds to nothing reads `±0` rather than `+0`.
    static func deviation(_ value: Double, of metric: RecoveryMetric) -> String {
        switch metric {
        case .sleepDuration:
            let minutes = Int((value / 60).rounded())
            guard minutes != 0 else { return "±0:00" }
            let size = abs(minutes)
            return "\(minutes > 0 ? "+" : "−")\(size / 60):\(String(format: "%02d", size % 60))"
        case .respiratoryRate, .temperatureDeviation:
            return (value * 10).rounded() == 0 ? "±0" : signed(value, fractionDigits: 1)
        case .readiness, .hrv, .lowestHeartRate, .sleepScore, .steps:
            return value.rounded() == 0 ? "±0" : signed(value.rounded(), fractionDigits: 0)
        }
    }

    /// The metric's name as a row label.
    static func metricName(_ metric: RecoveryMetric) -> String {
        switch metric {
        case .readiness: "Readiness"
        case .hrv: "HRV"
        case .lowestHeartRate: "Lowest HR"
        case .sleepDuration: "Asleep"
        case .sleepScore: "Sleep score"
        case .respiratoryRate: "Breathing"
        case .temperatureDeviation: "Temperature"
        case .steps: "Steps"
        }
    }

    /// The metric spelled out, for a trend's title and VoiceOver.
    static func metricTitle(_ metric: RecoveryMetric) -> String {
        switch metric {
        case .readiness: "Readiness"
        case .hrv: "Heart rate variability"
        case .lowestHeartRate: "Lowest heart rate"
        case .sleepDuration: "Time asleep"
        case .sleepScore: "Sleep score"
        case .respiratoryRate: "Breathing rate"
        case .temperatureDeviation: "Temperature deviation"
        case .steps: "Steps"
        }
    }

    /// Which night a row is, by the evening it began — Oura files a night under the morning it
    /// ended: "Last night · Mon, Nov 2" and "Night before · Sun, Nov 1" counted back from today,
    /// "Night of Mon, Nov 2" from any other day.
    static func nightCaption(_ night: RecentNights.Night, isLastNight: Bool, isToday: Bool) -> String {
        let evening = date(night.wakingDay.adding(days: -1))
        guard isToday else { return "Night of \(evening)" }
        return "\(isLastNight ? "Last night" : "Night before") · \(evening)"
    }

    // MARK: - Cardio and habits

    /// A time of day from minutes after midnight, on a 24-hour clock: `23:30`, `07:05`. Wraps, so a
    /// mean bedtime of 25:10 reads `01:10`.
    static func clockTime(minutesAfterMidnight minutes: Double) -> String {
        let whole = (Int(minutes.rounded()) % 1440 + 1440) % 1440
        return String(format: "%02d:%02d", whole / 60, whole % 60)
    }

    /// A change between two spans, signed with a true minus: `+12`, `−3.5`, `±0`.
    static func change(_ value: Double, fractionDigits: Int = 0) -> String {
        let scale = pow(10, Double(fractionDigits))
        return (value * scale).rounded() == 0 ? "±0" : signed(value, fractionDigits: fractionDigits)
    }

    /// Minutes in words, the way a sitting or a session length is read: `48 min`, `1 h 20 min`.
    static func minutes(_ value: Double) -> String {
        MeasurementText.spelledDuration(seconds: value * 60)
    }

    private static func whole(_ value: Double) -> String {
        value.formatted(.number.precision(.fractionLength(0)).grouping(.never))
    }

    /// A signed figure with a true minus sign, which sits at the width of a plus in a column.
    private static func signed(_ value: Double, fractionDigits: Int) -> String {
        let size = abs(value).formatted(.number.precision(.fractionLength(fractionDigits)))
        return value < 0 ? "−\(size)" : "+\(size)"
    }

    /// Sleep as `7h 12m`, which reads at a glance where `25,920 s` or `7:12` would not.
    static func hoursAndMinutes(seconds: Double) -> String {
        let minutes = Int((seconds / 60).rounded())
        return "\(minutes / 60)h \(String(format: "%02d", minutes % 60))m"
    }

    // MARK: - Records

    /// A weight unit spelled out, for the places a unit is chosen rather than read beside a figure.
    static func unitName(_ unit: WeightUnit) -> String {
        switch unit {
        case .kilograms: "Kilograms"
        case .pounds: "Pounds"
        case .unrecognised(let wire): "Unit \(wire)"
        }
    }

    /// An estimate in pounds, rounded to the whole number it can honestly claim and marked as one.
    static func estimate(_ value: Double, _ unit: WeightUnit) -> String {
        let parts = weightParts(value, unit)
        let figure = estimateFigure(Imperial.pounds(value, from: unit))
        return parts.unit.isEmpty ? "≈ \(figure)" : "≈ \(figure) \(parts.unit)"
    }

    /// An estimated single as a whole number, rounded the one way everywhere it is shown.
    static func estimateFigure(_ value: Double) -> String {
        value.formatted(.number.precision(.fractionLength(0)).grouping(.never))
    }

    /// The day a record was set, with its year: records are read across years.
    static func recordDate(_ instant: Date) -> String {
        instant.formatted(.dateTime.month(.abbreviated).day().year())
    }

    /// What a record holds, as its headline figure — `245 lb × 1`, `26:45`, `8:37 /mi`. A rep max
    /// reads at its own count, `405 lb × 6`, even when the set holding it was longer; the detail
    /// line says which set.
    static func achievement(_ achievement: Achievement) -> String {
        switch achievement {
        case .heaviestSet(let lift):
            self.lift(lift)
        case .repMax(let reps, let lift):
            "\(weight(lift.weight, lift.unit)) × \(reps)"
        case .bestSetVolume(let set):
            volume(set.volume, set.unit)
        case .bestSessionVolume(let session):
            volume(session.volume, session.unit)
        case .longestDistance(let effort):
            distance(effort.distance, effort.unit)
        case .fastestTime(_, let effort):
            clock(seconds: effort.duration)
        case .bestPace(let pace):
            self.pace(pace)
        case .unrecognised:
            "A record this version cannot show"
        }
    }

    /// The facts behind a record's headline, where it has any worth a second line.
    static func achievementDetail(_ achievement: Achievement) -> String? {
        switch achievement {
        case .heaviestSet(let lift):
            lift.reps > 1 ? "Estimated single \(estimate(lift.estimatedOneRepMax, lift.unit))" : nil
        case .repMax(let reps, let lift):
            lift.reps > reps
                ? "\(longerSet(lift.reps)) · estimated single \(estimate(lift.estimatedOneRepMax, lift.unit))"
                : "Estimated single \(estimate(lift.estimatedOneRepMax, lift.unit))"
        case .bestSetVolume(let set):
            "\(weight(set.weight, set.unit)) × \(set.reps)"
        case .bestSessionVolume, .unrecognised:
            nil
        case .longestDistance(let effort):
            "in \(clock(seconds: effort.duration))"
        case .fastestTime(_, let effort):
            distance(effort.distance, effort.unit)
        case .bestPace(let pace):
            "\(distance(pace.effort.distance, pace.effort.unit)) in \(clock(seconds: pace.effort.duration))"
        }
    }

    /// A fastest time's distance as a runner names it: a race distance by its name, any other in
    /// miles — or as logged, for a track's metres (`distance(_:_:)`). The bracket is the server's,
    /// the distance actually covered rounded to the metre, so only an exact race distance is named.
    static func raceDistance(bracketMeters: Int, _ effort: Achievement.Distance) -> String {
        switch bracketMeters {
        case 1609: "Mile"
        case 5000: "5K"
        case 10_000: "10K"
        case 21_097: "Half marathon"
        case 42_195: "Marathon"
        default: distance(effort.distance, effort.unit)
        }
    }

    /// One set as performed — `405 lb × 7`.
    static func lift(_ lift: Achievement.Lift) -> String {
        "\(weight(lift.weight, lift.unit)) × \(lift.reps)"
    }

    /// Where a record carried down from a longer set came from — `From a set of 7`.
    static func longerSet(_ reps: Int) -> String {
        "From a set of \(reps)"
    }

    /// What a logged set beat, as one sentence: the record most worth saying, and how many more —
    /// "New 6-rep max and 4 more records". A set beats a rep max at every count it holds, so
    /// naming each would read a set of twelve out as a list of eleven.
    static func recordsBeaten(_ outcome: SetRecordOutcome) -> String? {
        switch outcome {
        case .notApplicable, .noRecordYet, .none:
            nil
        case .beats(let first, let also):
            also.isEmpty
                ? "New \(first.label.lowercased())"
                : "New \(first.label.lowercased()) and \(count(also.count, "more record"))"
        }
    }

    /// A pace per mile, whichever unit the effort was logged in. The figure is the server's.
    static func pace(_ pace: Achievement.Pace) -> String {
        Imperial.pace(perMile: pace.perMile)
    }

    /// How a record came to be, from its own lineage: when it was set, and how long the one it
    /// replaced had stood. A predecessor that stood less than a day says nothing about it.
    static func recordLineage(_ record: PersonalRecord) -> String {
        let set = "Set \(recordDate(record.achievedAt))"
        switch record.daysPredecessorStood {
        case .none: return "\(set) · first on record"
        case .some(0): return set
        case .some(let days): return "\(set) · beat one that stood \(count(days, "day"))"
        }
    }
}

extension String {
    /// The string, or `nil` when it holds nothing but whitespace — for the many wire members where
    /// the server writes `""` and the domain means "absent".
    var nonEmpty: String? {
        let trimmed = trimmingCharacters(in: .whitespacesAndNewlines)
        return trimmed.isEmpty ? nil : trimmed
    }
}
