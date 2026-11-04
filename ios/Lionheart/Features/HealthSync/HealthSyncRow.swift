import SwiftUI

/// The Apple Health sync as one row: when it last finished, what it did, and a button to run one —
/// or to connect, until the athlete has let Lionheart read Health.
///
/// The sibling of `OuraSyncRow`, and shaped like it: the status a glance wants first, and the
/// server's own words beneath where something was refused.
struct HealthSyncRow: View {
    let store: HealthSyncStore

    var body: some View {
        let state = store.state

        VStack(alignment: .leading, spacing: 3) {
            ViewThatFits(in: .horizontal) {
                HStack(alignment: .firstTextBaseline, spacing: 8) {
                    name
                    statusText(state).fixedSize()
                    Spacer(minLength: 8)
                    control(state)
                }
                VStack(alignment: .leading, spacing: 2) {
                    HStack(alignment: .firstTextBaseline) {
                        name
                        Spacer(minLength: 8)
                        control(state)
                    }
                    statusText(state).fixedSize(horizontal: false, vertical: true)
                }
            }
            if let detail = detail(state) {
                Text(detail)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
                    .fixedSize(horizontal: false, vertical: true)
            }
        }
        .accessibilityElement(children: .combine)
        // Asked again while not connected: the measurement sync's row asks for the same Health sheet, and
        // connecting there connects this too.
        .task {
            switch store.state {
            case .unknown, .notConnected: await store.refreshStatus()
            case .unavailable, .idle, .syncing, .synced, .failed: break
            }
        }
    }

    private var name: some View {
        Text("Apple Health").font(.subheadline.weight(.semibold))
    }

    private func statusText(_ state: HealthSyncState) -> some View {
        Text(status(state))
            .font(.subheadline.monospacedDigit())
            .foregroundStyle(statusColour(state))
    }

    @ViewBuilder
    private func control(_ state: HealthSyncState) -> some View {
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

    private func status(_ state: HealthSyncState) -> String {
        switch state {
        case .unknown: "not checked yet"
        case .unavailable: "not on this device"
        case .notConnected: "not connected"
        case .idle(let lastSynced): lastSynced.map { "synced \(Self.when($0))" } ?? "never synced"
        case .syncing(let progress, _):
            progress.imported > 0 ? "syncing · \(Formatting.count(progress.imported, "workout")) imported" : "syncing"
        case .synced(let tally, let at):
            tally.imported > 0
                ? "\(Formatting.count(tally.imported, "workout")) imported · \(Self.when(at))"
                : "up to date · \(Self.when(at))"
        case .failed(let failure, _, _): failure.headline
        }
    }

    private func detail(_ state: HealthSyncState) -> String? {
        switch state {
        case .unknown, .unavailable, .idle, .syncing:
            nil
        case .notConnected:
            "Reads the runs, rides, hikes and walks other apps write to Health — Strava's, a watch's — with their routes, heart rate, power and running form. Read only."
        case .synced(let tally, _):
            Self.leftOut(tally)
        case .failed(let failure, let done, let lastSynced):
            [failure.detail, Self.leftOut(done), lastSynced.map { "Last synced \(Self.when($0))." }]
                .compactMap(\.self).joined(separator: " ").nonEmpty
        }
    }

    private func statusColour(_ state: HealthSyncState) -> Color {
        switch state {
        case .failed: .orange
        case .notConnected: Color(.label)
        case .unknown, .unavailable, .idle, .syncing, .synced: Color(.secondaryLabel)
        }
    }

    /// What the server left out and refused, in a sentence or two; nothing when it took everything.
    private static func leftOut(_ tally: HealthSyncTally) -> String? {
        let left = tally.leftOut
            .sorted { $0.value > $1.value }
            .map { "\($0.value) \($0.key.phrase)" }
        let parts = [
            left.isEmpty ? nil : "Left out: " + left.joined(separator: ", ") + ".",
            tally.refused.isEmpty ? nil : "Not taken yet: " + tally.refused.joined(separator: " ")
                + " Offered again on every sync.",
        ].compactMap(\.self)
        return parts.isEmpty ? nil : parts.joined(separator: " ")
    }

    /// "14:02" today, "3 Nov 14:02" before.
    private static func when(_ instant: Date) -> String {
        Calendar.current.isDateInToday(instant)
            ? instant.formatted(date: .omitted, time: .shortened)
            : instant.formatted(date: .abbreviated, time: .shortened)
    }
}
