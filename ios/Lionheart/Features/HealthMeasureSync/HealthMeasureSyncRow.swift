import SwiftUI

/// The measurement sync as one row: when it last finished, what it brought, and a button to run one
/// — or to connect, until the athlete has let Lionheart read everything it asks for.
struct HealthMeasureSyncRow: View {
    let store: HealthMeasureSyncStore

    var body: some View {
        let state = store.state

        VStack(alignment: .leading, spacing: 3) {
            HStack(alignment: .firstTextBaseline, spacing: 8) {
                Text("Apple Health").font(.subheadline.weight(.semibold))
                Text(status(state))
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(state.isFailed ? .orange : Color(.secondaryLabel))
                    .fixedSize(horizontal: false, vertical: true)
                Spacer(minLength: 8)
                control(state)
            }
            if let detail = detail(state) {
                Text(detail)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .accessibilityElement(children: .combine)
        // Asked again while not connected: the workout sync's row asks for the same Health sheet, and
        // connecting there connects this too.
        .task {
            switch store.state {
            case .unknown, .notConnected: await store.refreshStatus()
            case .unavailable, .idle, .syncing, .synced, .failed: break
            }
        }
    }

    @ViewBuilder
    private func control(_ state: HealthMeasureSyncState) -> some View {
        switch state {
        case .syncing:
            ProgressView().accessibilityLabel("Syncing")
        case .notConnected:
            Button("Connect") { Task { await store.connect() } }
                .buttonStyle(.borderless)
                .font(.subheadline)
        case .idle, .synced, .failed:
            Button("Sync") { Task { await store.sync() } }
                .buttonStyle(.borderless)
                .font(.subheadline)
        case .unknown, .unavailable:
            EmptyView()
        }
    }

    private func status(_ state: HealthMeasureSyncState) -> String {
        switch state {
        case .unknown: "not checked yet"
        case .unavailable: "not on this device"
        case .notConnected: "not connected"
        case .idle(let lastSynced): lastSynced.map { "synced \(Self.when($0))" } ?? "never synced"
        case .syncing(_, let through, _): through.map { "syncing · to \(Formatting.date($0))" } ?? "syncing"
        case .synced(let tally, let at):
            tally.daysRecorded > 0 ? "\(Formatting.count(tally.daysRecorded, "new day")) · \(Self.when(at))" : "up to date · \(Self.when(at))"
        case .failed(let message, _, _): message
        }
    }

    private func detail(_ state: HealthMeasureSyncState) -> String? {
        switch state {
        case .notConnected:
            "Reads what your Apple Watch and iPhone measure — VO₂max, resting heart rate, HRV, gait — with your weigh-ins and date of birth. Read only."
        case .synced(let tally, _), .failed(_, let tally, _):
            Self.tallyLine(tally)
        case .unknown, .unavailable, .idle, .syncing:
            nil
        }
    }

    /// What a sync did beyond new days, in a sentence; nothing when that was all.
    private static func tallyLine(_ tally: HealthMeasureTally) -> String? {
        [
            tally.bodyweightsAdded > 0
                ? "\(Formatting.count(tally.bodyweightsAdded, "bodyweight")) filled in from Health"
                    + (tally.setsReweighed > 0 ? "; the system load of \(Formatting.count(tally.setsReweighed, "weighted dip or pull-up set", plural: "weighted dip and pull-up sets")) changed." : ".")
                : nil,
            tally.daysUnreadable > 0 ? "\(Formatting.count(tally.daysUnreadable, "day")) refused as unreadable." : nil,
            // Only when it is the whole story: Oura writes its own HRV to Health on every night, and
            // saying so every sync is noise; a sync that kept nothing and left readings out is not.
            tally.daysRecorded + tally.daysRestated == 0 && tally.readingsFromOtherApps > 0
                ? "\(Formatting.count(tally.readingsFromOtherApps, "reading")) left out: written by other apps, not measured by your Apple Watch or iPhone."
                : nil,
        ].compactMap(\.self).joined(separator: " ").nonEmpty
    }

    private static func when(_ instant: Date) -> String {
        Calendar.current.isDateInToday(instant)
            ? instant.formatted(date: .omitted, time: .shortened)
            : instant.formatted(date: .abbreviated, time: .shortened)
    }
}

private extension HealthMeasureSyncState {
    var isFailed: Bool {
        if case .failed = self { true } else { false }
    }
}
