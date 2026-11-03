import Charts
import SwiftUI

/// Every Oura day on record, newest first, with readiness and sleep charted above them.
///
/// Only days the server actually stored appear. The single-day route synthesizes a zeroed
/// placeholder when a day has no data, so a gap in this list is a gap in the data rather than a
/// zero — which is the distinction a chart of synthesized zeroes would destroy.
struct OuraDaysView: View {
    let days: [OuraDay]
    /// The tab's own reload. Passed in rather than holding the store, because this screen reads a
    /// slice of the snapshot and has nothing to write.
    let refresh: () async -> Void

    var body: some View {
        List {
            if days.isEmpty {
                ContentUnavailableView {
                    Text("No Oura days")
                } description: {
                    Text("Days appear here once the ring has synced. Only days with real data are shown — a gap is a gap, not a zero.")
                }
                .listRowBackground(Color.clear)
            } else {
                scoresSection
                Section("Days") {
                    ForEach(days) { day in
                        NavigationLink(value: BodyRoute.ouraDay(day.date)) {
                            OuraDayRow(day: day, today: today)
                        }
                        // Copying is the whole menu, and that is the data's doing rather than a gap
                        // here: an Oura day is written by the ring through the sync and there is no
                        // route that edits or removes one.
                        .contextMenu {
                            Button {
                                Clipboard.copy(Self.summary(of: day))
                            } label: {
                                Label("Copy", systemImage: "doc.on.doc")
                            }
                        }
                    }
                }
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Oura days")
        .refreshable { await refresh() }
    }

    /// One day as a line of text: which day, and every score it carries.
    static func summary(of day: OuraDay) -> String {
        let scores = OuraPanel.panels(of: day).compactMap { panel in
            panel.score.map { "\(panel.title) \($0)" }
        }
        let when = Formatting.date(day.date)
        return scores.isEmpty ? "\(when) · recorded, not scored"
            : "\(when) · \(scores.joined(separator: " · "))"
    }

    @ViewBuilder
    private var scoresSection: some View {
        let points = Self.scorePoints(in: days)
        if points.count >= 2 {
            Section {
                Chart(points) { point in
                    LineMark(
                        x: .value("Day", point.day.middayDate()),
                        y: .value("Score", point.score),
                        series: .value("Measure", point.measure))
                    .foregroundStyle(by: .value("Measure", point.measure))
                }
                .chartYScale(domain: 0...100)
                .chartForegroundStyleScale([
                    "Readiness": Color.green,
                    "Sleep": Color.indigo,
                ])
                .frame(height: 170)
                .padding(.vertical, 8)
                .accessibilityLabel("Readiness and sleep scores over time")
            } header: {
                Text("Scores")
            } footer: {
                Text("Days the ring did not sync are absent rather than drawn as zero.")
            }
        }
    }

    /// Readiness and sleep as one charted series set.
    ///
    /// A day with no score for a measure contributes no point for it, so the line breaks where the
    /// ring was off rather than diving to the axis. Every score the server sent is plotted,
    /// including a zero: absence is the server's answer for absence, so a zero is one Oura issued.
    static func scorePoints(in days: [OuraDay]) -> [ScorePoint] {
        days.flatMap { day -> [ScorePoint] in
            var points: [ScorePoint] = []
            if let score = day.readiness?.score {
                points.append(ScorePoint(day: day.date, measure: "Readiness", score: score.rounded))
            }
            if let score = day.sleep?.score {
                points.append(ScorePoint(day: day.date, measure: "Sleep", score: score.rounded))
            }
            return points
        }
    }

    struct ScorePoint: Identifiable {
        let day: CalendarDay
        let measure: String
        let score: Int

        var id: String { "\(day.wire)-\(measure)" }
    }

    private var today: CalendarDay { CalendarDay(Date()) }
}

/// One Oura day in a list: the scores it carries, and nothing for the panels it does not.
private struct OuraDayRow: View {
    let day: OuraDay
    let today: CalendarDay

    var body: some View {
        VStack(alignment: .leading, spacing: 4) {
            Text(Formatting.day(RelativeDay.of(day.date, today: today)))
                .font(.headline)
            if scores.isEmpty {
                // The common case for today: the ring has filed something, and Oura issues the
                // day's scores later. The day still opens, because what it did file is on it.
                Text("Recorded, but not scored")
                    .font(.subheadline)
                    .foregroundStyle(Color(.secondaryLabel))
            } else {
                Text(scores.joined(separator: " · "))
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }

    /// The scored collections the day carries. A collection without a score — stress, blood
    /// oxygen, resilience — contributes nothing to this line; it is not missing, it simply has
    /// measurements rather than a score, and the day's own screen shows them.
    private var scores: [String] {
        OuraPanel.panels(of: day).compactMap { panel in
            panel.score.map { "\(panel.title) \($0)" }
        }
    }
}
