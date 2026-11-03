import SwiftUI

/// The numbers, by movement.
///
/// Two records per movement — heaviest weight, and most weight times reps in one set — because the
/// server tracks exactly those two and they answer different questions. Volume is printed as the
/// server sent it, never recomputed here: one number, one source.
///
/// Empty is the expected state on a fresh database and says so plainly. It is also, today, the
/// *only* state: nothing on the server writes a `PersonalRecord`, so until that write path lands
/// this screen is honest about having nothing rather than implying the athlete has set no records.
struct PersonalRecordsView: View {
    let records: [MovementRecords]

    var body: some View {
        List {
            if records.isEmpty {
                ContentUnavailableView {
                    Label("No records yet", systemImage: "trophy")
                } description: {
                    Text("A record appears here when a logged set beats your best on that movement.")
                }
                .listRowBackground(Color.clear)
            } else {
                ForEach(records) { movement in
                    Section(movement.movement?.displayName ?? "Unknown movement") {
                        if let strength = movement.strength {
                            RecordRow(record: strength)
                        }
                        if let volume = movement.volume {
                            RecordRow(record: volume)
                        }
                    }
                }
            }
        }
        .listStyle(.insetGrouped)
        .navigationTitle("Personal records")
    }
}

/// One standing record, with how long the last one stood.
///
/// "Previously" is the fact that turns a number into progress. It is absent on a first record,
/// which is why it is an optional date and not a zero-length gap.
private struct RecordRow: View {
    let record: PersonalRecord

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            LabeledContent {
                Text(headline)
                    .font(.headline.monospacedDigit())
            } label: {
                Text(record.kind.label)
            }

            if let achievedAt = record.achievedAt {
                Text(subtitle(achievedAt))
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
        .accessibilityLabel("\(record.kind.label) record, \(headline)")
    }

    private var headline: String {
        switch record.kind {
        case .strength, .unrecognised:
            "\(MeasurementText.plain(record.weight)) \(record.unit.abbreviation) × \(record.reps)"
        case .volume:
            "\(MeasurementText.plain(record.volume)) \(record.unit.abbreviation) (\(MeasurementText.plain(record.weight)) × \(record.reps))"
        }
    }

    private func subtitle(_ achievedAt: Date) -> String {
        let set = "Set \(achievedAt.formatted(.dateTime.month(.abbreviated).day().year()))"
        guard let previous = record.previousAchievedAt else { return "\(set) · first on record" }
        let days = Int(achievedAt.timeIntervalSince(previous) / 86_400)
        guard days > 0 else { return set }
        return "\(set) · beat a record that stood \(Formatting.count(days, "day"))"
    }
}
