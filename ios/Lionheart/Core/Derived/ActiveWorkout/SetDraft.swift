import Foundation

/// The entry row's fields, as typed, and the set they amount to.
///
/// It holds **strings** rather than numbers, which is the whole reason it exists. A `Double` bound
/// to a text field fights the athlete: clearing the field is zero, typing "10" on the way to "102"
/// is a weight of ten, and a half-typed "1." is not a number at all. Keeping the text and deciding
/// separately whether it amounts to a set is what makes typing feel like typing.
///
/// `command` is that decision, and `nil` is a refusal rather than a failure: the Log button is
/// disabled, nothing is said, and the athlete is already looking at the field that is empty.
struct SetDraft: Equatable, Sendable {
    var modality: SetModality
    /// The movement these fields belong to. A trail run and a freeform set have none, which is the
    /// domain's position and not an omission.
    var movementDataId: UUID?

    var weight: String = ""
    var reps: String = ""
    var weightUnit: WeightUnit = .kilograms

    var distance: String = ""
    var distanceUnit: DistanceUnit = .kilometers
    /// Duration as two fields, which is how a time is read off a treadmill: `24` and `30`, not
    /// `1470` seconds.
    var minutes: String = ""
    var seconds: String = ""
    var elevationGainMeters: String = ""

    var rpe: Rpe = .unrated
    /// What the lift is for. Working unless the athlete marks it otherwise; only a lift has one.
    var kind: SetKind = .working
    var text: String = ""

    /// Seeds the fields from a set — the previous one on this movement, or its target.
    ///
    /// A zero measure comes through as an empty field, because a zero distance or a zero duration
    /// is a set that did not happen. **A lift's weight is the exception**: nought is what a pull-up,
    /// a dip and a push-up weigh, and emptying it here cost a typed "0" on every single set of every
    /// bodyweight movement, which is the opposite of the one-tap next set this whole type exists
    /// for. A weight that is only a placeholder is emptied by `init(next:in:)` instead.
    init(from command: SetCommand) {
        modality = command.modality
        movementDataId = command.movementDataId
        rpe = command.rpe

        switch command {
        case .lift(let set):
            weight = Self.weightField(set.weight)
            reps = Self.field(set.reps)
            weightUnit = set.unit
            kind = set.kind
        case .distanceTime(let set):
            distance = Self.field(set.distance)
            distanceUnit = set.unit
            (minutes, seconds) = Self.fields(duration: set.duration)
        case .trailRun(let set):
            distance = Self.field(set.distance)
            distanceUnit = set.unit
            (minutes, seconds) = Self.fields(duration: set.duration)
            elevationGainMeters = Self.field(set.elevationGainMeters)
        case .freeform(let words):
            text = words
        }
    }

    /// The entry row for the next set on a movement, by the carry-forward rule in `SetDefaults`.
    ///
    /// Where no weight carries forward — the first set on a movement just picked, or a target with
    /// no load — the weight starts empty. Its nought would be a placeholder rather than a set that
    /// happened, and typing over it gives "0100".
    init(next movementDataId: UUID, in workout: ActiveWorkout, previous: PreviousPerformance? = nil) {
        self.init(from: SetDefaults.nextSet(on: movementDataId, in: workout, previous: previous))
        if !SetDefaults.carriesWeightForward(on: movementDataId, in: workout, previous: previous) {
            weight = ""
        }
    }

    /// The set these fields amount to, or `nil` while they do not amount to one yet.
    ///
    /// What each shape insists on is what makes it that shape: a lift with no reps has not
    /// happened, and distance work with no distance is a duration rather than a set. RPE is never
    /// insisted on — it is a case, and leaving it unrated is a legitimate answer.
    var command: SetCommand? {
        switch modality {
        case .lift:
            guard let movementDataId, let reps = Self.whole(reps), reps > 0,
                  let weight = Self.number(weight)
            else { return nil }
            return .lift(SetCommand.Lift(
                movementDataId: movementDataId,
                reps: reps,
                weight: weight,
                unit: weightUnit,
                rpe: rpe,
                kind: kind))

        case .distanceTime:
            guard let movementDataId, let distance = Self.number(distance), distance > 0
            else { return nil }
            return .distanceTime(SetCommand.DistanceTime(
                movementDataId: movementDataId,
                distance: distance,
                unit: distanceUnit,
                duration: duration,
                rpe: rpe))

        case .trailRun:
            guard let distance = Self.number(distance), distance > 0 else { return nil }
            return .trailRun(SetCommand.TrailRun(
                distance: distance,
                unit: distanceUnit,
                duration: duration,
                elevationGainMeters: Self.number(elevationGainMeters) ?? 0,
                rpe: rpe))

        case .freeform:
            let words = text.trimmingCharacters(in: .whitespacesAndNewlines)
            return words.isEmpty ? nil : .freeform(text: words)
        }
    }

    /// Seconds from the two fields. Either may be empty, so "5" minutes alone is five minutes.
    var duration: TimeInterval {
        (Self.number(minutes) ?? 0) * 60 + (Self.number(seconds) ?? 0)
    }

    /// Whether these fields are ready to be logged. The Log button reads this.
    var isLoggable: Bool { command != nil }

    // MARK: - Text and numbers

    /// A number as typed, accepting either decimal separator.
    ///
    /// The decimal keyboard shows the athlete's own locale's separator, so a comma is what a
    /// European athlete will type and `Double("102,5")` is nil. Accepting both is one line here and
    /// a weight that silently refuses to be entered otherwise.
    static func number(_ text: String) -> Double? {
        let normalised = text
            .trimmingCharacters(in: .whitespaces)
            .replacingOccurrences(of: ",", with: ".")
        return normalised.isEmpty ? nil : Double(normalised)
    }

    static func whole(_ text: String) -> Int? {
        number(text).map { Int($0.rounded()) }
    }

    /// A number as a field: empty for zero, and without a trailing `.0`.
    ///
    /// Formatted here rather than through `Support/Formatting`, which is presentation and which
    /// `Core/` does not reach into. Plate maths gives halves and quarters and nothing finer, so two
    /// decimals is the whole range a weight needs.
    private static func field(_ value: Double) -> String {
        value == 0
            ? ""
            : value.formatted(.number.precision(.fractionLength(0...2)).grouping(.never))
    }

    /// A lift's weight, where nought is a value rather than an absence — see `init(from:)`.
    private static func weightField(_ value: Double) -> String {
        value.formatted(.number.precision(.fractionLength(0...2)).grouping(.never))
    }

    private static func field(_ value: Int) -> String {
        value == 0 ? "" : "\(value)"
    }

    private static func fields(duration: TimeInterval) -> (String, String) {
        guard duration > 0 else { return ("", "") }
        let whole = Int(duration.rounded())
        return ("\(whole / 60)", whole % 60 == 0 ? "" : "\(whole % 60)")
    }
}
