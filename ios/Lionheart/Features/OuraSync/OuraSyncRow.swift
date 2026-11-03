import SwiftUI

/// The Oura sync control: how far the stored data reaches, what the last sync did, and a button to
/// run one.
///
/// One row rather than a banner, because it is a status line the athlete glances at, and it says
/// the most important thing first — how current the data under it is. A failure keeps the row's
/// shape and adds the server's own sentence beneath, which is where a rejected Oura token is
/// named.
struct OuraSyncRow: View {
    let store: OuraSyncStore
    let today: CalendarDay

    var body: some View {
        let state = store.state

        VStack(alignment: .leading, spacing: 3) {
            // The status beside the name when it fits on the line, under it when it does not —
            // the larger text sizes, or a long "stored · through · behind" — so it never truncates.
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
    }

    private var name: some View {
        Text("Oura").font(.subheadline.weight(.semibold))
    }

    private func statusText(_ state: OuraSyncState) -> some View {
        Text(status(state))
            .font(.subheadline.monospacedDigit())
            .foregroundStyle(statusColour(state))
    }

    @ViewBuilder
    private func control(_ state: OuraSyncState) -> some View {
        if state.isSyncing {
            ProgressView()
                .accessibilityLabel("Syncing")
        } else {
            Button("Sync") { Task { await store.sync() } }
                .buttonStyle(.borderless)
                .font(.subheadline)
        }
    }

    private func status(_ state: OuraSyncState) -> String {
        switch state {
        case .unknown:
            "not checked yet"
        case .idle(let coverage):
            reach(coverage)
        case .syncing(let range, _):
            "syncing \(span(range))"
        case .synced(let report, let coverage):
            "\(Formatting.count(report.stored, "document")) stored · \(reach(coverage))"
        case .failed(let failure, _):
            failure.headline
        }
    }

    private func detail(_ state: OuraSyncState) -> String? {
        switch state {
        case .unknown, .idle, .syncing:
            nil
        case .synced(let report, _):
            report.rejected > 0
                ? "\(Formatting.count(report.rejected, "document")) Oura sent could not be read."
                : nil
        case .failed(let failure, let coverage):
            [failure.detail, coverage.map { "Stored \(reach($0))." }].compactMap(\.self).joined(separator: " ")
        }
    }

    private func statusColour(_ state: OuraSyncState) -> Color {
        switch state {
        case .failed: .orange
        case .idle(let coverage) where OuraSyncPlan.isBehind(coverage, today: today): Color(.label)
        case .unknown, .idle, .syncing, .synced: Color(.secondaryLabel)
        }
    }

    /// How far the data reaches, in the words a glance wants: "data through today", "data through
    /// Sat, Aug 1 · 94 days behind".
    private func reach(_ coverage: OuraCoverage) -> String {
        switch coverage {
        case .nothingStored:
            return "nothing stored"
        case .through(let day, let readinessFiled):
            let when = switch RelativeDay.of(day, today: today) {
            case .today: "today"
            case .yesterday: "yesterday"
            case .tomorrow, .daysAgo, .inDays, .distant: Formatting.date(day)
            }
            let behind = day.days(until: today)
            if behind > 1 { return "data through \(when) · \(behind) days behind" }
            return readinessFiled ? "data through \(when)" : "data through \(when) · no readiness yet"
        }
    }

    private func span(_ range: DayRange) -> String {
        range.from == range.to
            ? Formatting.date(range.from)
            : "\(Formatting.date(range.from)) – \(Formatting.date(range.to))"
    }
}
