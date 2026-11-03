import SwiftUI

/// The row a set is typed into.
///
/// It sits inside the movement's own section rather than in a sheet, which is the decision the
/// whole screen is arranged around: a sheet costs a present, a dismiss and a loss of the list
/// behind it on every set, and a set is the thing the athlete does twenty times in an hour. In the
/// list, logging the next identical set is one tap on a button that has not moved.
///
/// The fields are shaped by `draft.modality`, and the two shapes a movement can carry are offered
/// as a segmented control — the same flow logs five at a hundred kilos and five kilometres in
/// twenty-four minutes, because `SetEntry` makes them siblings and the screen should not pretend
/// otherwise.
struct SetEntryForm: View {
    @Binding var draft: SetDraft
    /// What the button says. "Log set" while logging; the edit sheet saves instead.
    var actionLabel = "Log set"
    let log: (SetCommand) -> Void

    /// The RPE values worth offering. Below five is not a working set, and the server's scale is
    /// [0, 10] in halves.
    private static let ratings: [Double] = [5, 5.5, 6, 6.5, 7, 7.5, 8, 8.5, 9, 9.5, 10]

    /// Which field has the keyboard. Held so the keyboard toolbar can put it away — a number pad
    /// has no Return key, so without this there is no way off it but tapping elsewhere.
    @FocusState private var focused: Field?

    private enum Field: Hashable {
        case weight, reps, distance, minutes, seconds, elevation, text
    }

    var body: some View {
        VStack(alignment: .leading, spacing: 10) {
            if draft.movementDataId != nil { modalityPicker }

            switch draft.modality {
            case .lift: liftFields
            case .distanceTime: distanceFields
            case .trailRun: trailRunFields
            case .freeform: freeformField
            }

            HStack(spacing: Tokens.cardPadding) {
                if draft.modality != .freeform { ratingMenu }
                Spacer(minLength: 0)
                logButton
            }
        }
        .padding(.vertical, 6)
        .toolbar { keyboardBar }
    }

    /// Log and Done, above the keys.
    ///
    /// Found by looking: with the number pad up, the rest bar — which rises with the keyboard —
    /// sits exactly where the Log button is, so the tap that logs the next set landed on "+30s"
    /// instead. The rest bar is right to be there, so the button is given a second home that
    /// nothing can cover. It is also the only way off a number pad, which has no Return key.
    @ToolbarContentBuilder
    private var keyboardBar: some ToolbarContent {
        ToolbarItemGroup(placement: .keyboard) {
            Button("Done") { focused = nil }
            Spacer()
            Button(actionLabel) {
                if let command = draft.command { log(command) }
            }
            .fontWeight(.semibold)
            .disabled(!draft.isLoggable)
        }
    }

    // MARK: - Shapes

    private var modalityPicker: some View {
        Picker("How this set is measured", selection: $draft.modality) {
            Text("Weight & reps").tag(SetModality.lift)
            Text("Distance & time").tag(SetModality.distanceTime)
        }
        .pickerStyle(.segmented)
        .labelsHidden()
    }

    private var liftFields: some View {
        HStack(alignment: .bottom, spacing: Tokens.cardPadding) {
            // Empty is the start of a movement with no load to carry forward, and a lift needs a
            // weight to be logged — so the field says how to log one that has none. Short because
            // the field is a third of the row: "0 for bodyweight" truncated to "0 for bodyw…".
            field(
                "Weight", text: $draft.weight, keyboard: .decimalPad, focus: .weight,
                prompt: "0 if none")
            weightUnitMenu
            field("Reps", text: $draft.reps, keyboard: .numberPad, focus: .reps)
        }
    }

    private var distanceFields: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .bottom, spacing: Tokens.cardPadding) {
                field("Distance", text: $draft.distance, keyboard: .decimalPad, focus: .distance)
                distanceUnitMenu
            }
            durationFields
        }
    }

    private var trailRunFields: some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .bottom, spacing: Tokens.cardPadding) {
                field("Distance", text: $draft.distance, keyboard: .decimalPad, focus: .distance)
                distanceUnitMenu
            }
            durationFields
            field(
                "Elevation gain (m)", text: $draft.elevationGainMeters,
                keyboard: .numbersAndPunctuation, focus: .elevation)
        }
    }

    private var durationFields: some View {
        HStack(alignment: .bottom, spacing: Tokens.cardPadding) {
            field("Minutes", text: $draft.minutes, keyboard: .numberPad, focus: .minutes)
            field("Seconds", text: $draft.seconds, keyboard: .numberPad, focus: .seconds)
        }
    }

    private var freeformField: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("What you did")
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))
            TextField("Ten minutes of hip mobility", text: $draft.text, axis: .vertical)
                .textFieldStyle(.roundedBorder)
                .lineLimit(1...3)
                .focused($focused, equals: .text)
        }
    }

    // MARK: - Pieces

    /// A labelled number field.
    ///
    /// The caption sits above rather than inside as a placeholder, because a placeholder disappears
    /// the moment the field is filled and "102.5" with nothing saying what it is is exactly the
    /// kind of ambiguity a gym screen cannot afford.
    private func field(
        _ label: String, text: Binding<String>, keyboard: UIKeyboardType, focus: Field,
        prompt: String? = nil
    ) -> some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(label)
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))
            TextField(label, text: text, prompt: Text(prompt ?? label))
                .textFieldStyle(.roundedBorder)
                .keyboardType(keyboard)
                .multilineTextAlignment(.center)
                .monospacedDigit()
                .focused($focused, equals: focus)
                .accessibilityLabel(label)
        }
    }

    private var weightUnitMenu: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Unit")
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))
            Menu {
                Picker("Unit", selection: $draft.weightUnit) {
                    Text("kg").tag(WeightUnit.kilograms)
                    Text("lb").tag(WeightUnit.pounds)
                }
            } label: {
                Text(draft.weightUnit.abbreviation)
                    .frame(minWidth: 28)
            }
            .buttonStyle(.bordered)
            .accessibilityLabel("Weight unit, \(draft.weightUnit.abbreviation)")
        }
    }

    private var distanceUnitMenu: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text("Unit")
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))
            Menu {
                Picker("Unit", selection: $draft.distanceUnit) {
                    Text("km").tag(DistanceUnit.kilometers)
                    Text("mi").tag(DistanceUnit.miles)
                    Text("m").tag(DistanceUnit.meters)
                    Text("yd").tag(DistanceUnit.yards)
                }
            } label: {
                Text(draft.distanceUnit.abbreviation)
                    .frame(minWidth: 28)
            }
            .buttonStyle(.bordered)
            .accessibilityLabel("Distance unit, \(draft.distanceUnit.abbreviation)")
        }
    }

    /// RPE, left alone by default.
    ///
    /// A `Menu` rather than a row of buttons because it costs one tap to change and none to keep,
    /// and because unrated is a legitimate answer the athlete should not have to dismiss.
    private var ratingMenu: some View {
        Menu {
            Button("Not rated") { draft.rpe = .unrated }
            ForEach(Self.ratings, id: \.self) { value in
                Button(Formatting.measure(value)) { draft.rpe = .rated(value) }
            }
        } label: {
            Label(
                draft.rpe.value.map { "RPE \(Formatting.measure($0))" } ?? "RPE",
                systemImage: "gauge.with.dots.needle.33percent")
                .font(.subheadline)
        }
        .accessibilityLabel(draft.rpe.value.map { Formatting.rpe($0) } ?? "Rate of perceived exertion, not rated")
    }

    private var logButton: some View {
        Button {
            if let command = draft.command { log(command) }
        } label: {
            Text(actionLabel)
                .fontWeight(.semibold)
                .frame(minWidth: 96)
        }
        .buttonStyle(.borderedProminent)
        .disabled(!draft.isLoggable)
    }
}
