import SwiftUI

/// Everything one movement holds — or trail running — arranged the way it is read.
///
/// Lifting records come one weight unit at a time, behind a segmented control when the movement
/// holds both: a pounds record and a kilograms record of the same kind are two records, and putting
/// them side by side under one heading invites exactly the comparison the server refuses to make.
/// The unit lifted in most recently is shown first.
///
/// Tapping a record opens its chain; a long press offers retraction for the one that stands.
struct SubjectRecordsView: View {
    let subject: RecordSubject
    /// `nil` once nothing stands on the subject — the last record was retracted, or a refresh
    /// found it gone.
    let held: SubjectRecords?
    let catalogue: MovementCatalogue
    let store: HistoryStore

    @State private var asked: PersonalRecord?
    /// The chain a grid cell opened. See `RepMaxGridView.open` for why the grid cannot push with a
    /// link of its own.
    @State private var opened: RecordKey?

    var body: some View {
        let name = subject.name(in: catalogue)

        List {
            if let held {
                SubjectRecordsSections(held: held, name: name, ask: { asked = $0 }, open: { opened = $0 })
            } else {
                ContentUnavailableView {
                    Text("No standing records")
                } description: {
                    Text("\(name) holds no records. The next completed session that sets one is listed here.")
                }
                .listRowBackground(Color.clear)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle(name)
        .refreshable { await store.refresh() }
        .retractionDialog(asking: $asked, name: name, store: store)
        .navigationDestination(item: $opened) { key in
            RecordHistoryView(key: key, name: name, store: store)
        }
    }
}

/// One subject's standing records as list sections — the unit control, the bests, the rep-max grid
/// and the distance records — for any list that shows them: the records screen, and a movement's
/// page.
///
/// The list it sits in owns the retraction dialog and the push to a chain, because a dialog or a
/// destination hung inside a section belongs to whichever row SwiftUI happened to keep.
struct SubjectRecordsSections: View {
    let held: SubjectRecords
    let name: String
    let ask: (PersonalRecord) -> Void
    /// Opens a grid cell's chain. The rows push with a `HistoryRoute` link of their own.
    let open: (RecordKey) -> Void

    /// Which unit's lifting records are shown. View state: the athlete looking at their kilogram
    /// numbers is not a fact about their training. `nil` means "the most recent", which is also
    /// what a unit that has since lost all its records falls back to.
    @State private var chosenUnit: WeightUnit?

    var body: some View {
        content(RecordReading.of(held), name: name)
    }

    @ViewBuilder
    private func content(_ reading: RecordReading, name: String) -> some View {
        let lifting = shownLifting(in: reading)

        if reading.lifting.count > 1 {
            Section {
                Picker("Unit", selection: unitSelection(reading)) {
                    ForEach(reading.lifting) { held in
                        Text(Formatting.unitName(held.unit)).tag(held.unit)
                    }
                }
                .pickerStyle(.segmented)
            } footer: {
                Text("Each unit keeps its own records. Weights never convert, so switching units starts new records rather than comparing against the old ones.")
            }
        }

        if let lifting {
            liftingSections(lifting, name: name)
        }

        if let endurance = reading.endurance {
            enduranceSections(endurance, name: name)
        }

        if !reading.unrecognised.isEmpty {
            Section("Other records") {
                ForEach(reading.unrecognised) { record in
                    RecordRow(record: record, name: name, ask: ask)
                }
            }
        }
    }

    // MARK: - Lifting

    @ViewBuilder
    private func liftingSections(_ lifting: RecordReading.Lifting, name: String) -> some View {
        // The heaviest set is the grid's first cell, so it is a row here only when there is no grid
        // to hold it — showing it in both would show one number twice.
        let heaviestRow = lifting.grid.holdsRepMaxes ? nil : lifting.heaviestSet

        Section {
            if let heaviestRow {
                RecordRow(record: heaviestRow, name: name, ask: ask)
            }
            // Not repeated when it comes from the heaviest set's row, which already states it.
            if let estimate = lifting.strongestEstimate,
               estimate.id != heaviestRow?.id,
               let single = estimate.achievement.estimatedOneRepMax {
                EstimateRow(record: estimate, single: single, unit: lifting.unit)
            }
            if let best = lifting.bestSetVolume {
                RecordRow(record: best, name: name, ask: ask)
            }
            if let session = lifting.bestSessionVolume {
                RecordRow(record: session, name: name, ask: ask)
            }
        } header: {
            // Every figure reads in pounds; a movement also logged in kilograms keeps those records
            // apart, as the server ranks them, and says which these are.
            Text(lifting.unit == .kilograms ? "Best, logged in kilograms" : "Best")
        }

        if lifting.grid.holdsRepMaxes {
            Section {
                RepMaxGridView(grid: lifting.grid, name: name, ask: ask, open: open)
            } header: {
                Text("Rep maxes")
            } footer: {
                Text("Heaviest load at each rep count, lb. A set holds its own count and every count below it; the 1RM is the heaviest set. Filled: set in the latest session that set any.")
            }
        }
    }

    // MARK: - Endurance

    @ViewBuilder
    private func enduranceSections(_ endurance: RecordReading.Endurance, name: String) -> some View {
        if endurance.longestDistance != nil || endurance.bestPace != nil {
            Section("Distance") {
                if let longest = endurance.longestDistance {
                    RecordRow(record: longest, name: name, ask: ask)
                }
                if let pace = endurance.bestPace {
                    RecordRow(record: pace, name: name, ask: ask)
                }
            }
        }
        if !endurance.fastestTimes.isEmpty {
            Section {
                ForEach(endurance.fastestTimes) { record in
                    RecordRow(record: record, name: name, ask: ask)
                }
            } header: {
                Text("Fastest times")
            } footer: {
                Text("Timed at the distance you actually covered, so a 10K sets no 5K time. Race distances go by their names; the rest read in miles.")
            }
        }
    }

    // MARK: - Choosing the unit

    private func shownLifting(in reading: RecordReading) -> RecordReading.Lifting? {
        reading.lifting.first { $0.unit == chosenUnit } ?? reading.lifting.first
    }

    private func unitSelection(_ reading: RecordReading) -> Binding<WeightUnit> {
        Binding(
            get: { shownLifting(in: reading)?.unit ?? .kilograms },
            set: { chosenUnit = $0 })
    }

}

// MARK: - Rows

/// One standing record: what it is, what holds it, and how it came to be.
private struct RecordRow: View {
    let record: PersonalRecord
    let name: String
    let ask: (PersonalRecord) -> Void

    var body: some View {
        let kind = record.achievement.kind

        Group {
            if kind.hasHistory {
                NavigationLink(value: HistoryRoute.recordHistory(record.key)) { content }
            } else {
                content
            }
        }
        .contextMenu { RecordMenu(record: record, name: name, ask: ask) }
    }

    private var content: some View {
        VStack(alignment: .leading, spacing: 3) {
            LabeledContent {
                Text(Formatting.achievement(record.achievement))
                    .font(.headline.monospacedDigit())
                    .foregroundStyle(Color(.label))
            } label: {
                Text(label)
            }
            if let detail {
                Text(detail)
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
            Text(Formatting.recordLineage(record))
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }

    /// The second line, unless the label already said it — a fastest time's detail is its
    /// distance, which is its label here.
    private var detail: String? {
        switch record.achievement {
        case .fastestTime: nil
        case .heaviestSet, .bestSetVolume, .repMax, .bestSessionVolume, .longestDistance, .bestPace,
             .unrecognised:
            Formatting.achievementDetail(record.achievement)
        }
    }

    /// A fastest time is labelled by its distance, since a list of five "Fastest time" rows says
    /// nothing until the figures are read.
    private var label: String {
        switch record.achievement {
        case .fastestTime(let meters, let effort): Formatting.raceDistance(bracketMeters: meters, effort)
        case .heaviestSet, .bestSetVolume, .repMax, .bestSessionVolume, .longestDistance, .bestPace,
             .unrecognised:
            record.achievement.kind.label
        }
    }
}

/// The heaviest single the standing records imply, by the server's own estimates.
///
/// Not a record of its own — nothing on the server is held as an estimate — so it has no menu and
/// opens the chain of the record it was read from.
private struct EstimateRow: View {
    let record: PersonalRecord
    let single: Double
    let unit: WeightUnit

    var body: some View {
        NavigationLink(value: HistoryRoute.recordHistory(record.key)) {
            VStack(alignment: .leading, spacing: 3) {
                LabeledContent {
                    Text(Formatting.estimate(single, unit))
                        .font(.headline.monospacedDigit())
                        .foregroundStyle(Color(.label))
                } label: {
                    Text("Estimated one-rep max")
                }
                // The set as performed, since the estimate is the whole set's: a six-rep max carried
                // from a set of seven estimates from all seven.
                if let set = record.achievement.performedSet {
                    Text("From your set of \(Formatting.lift(set))")
                        .font(.caption)
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
            .padding(.vertical, 2)
            .accessibilityElement(children: .combine)
        }
    }
}
