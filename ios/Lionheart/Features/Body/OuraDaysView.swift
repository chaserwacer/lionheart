import Charts
import SwiftUI

/// Every Oura day on record, newest first, with readiness and sleep charted above them.
///
/// Only days the server actually stored appear. The single-day route synthesizes a zeroed
/// placeholder when a day has no data, so a gap in this list is a gap in the data rather than a
/// zero — which is the distinction a chart of synthesized zeroes would destroy.
struct OuraDaysView: View {
    let days: [OuraDay]

    var body: some View {
        List {
            if days.isEmpty {
                ContentUnavailableView {
                    Label("No Oura days yet", systemImage: "moon.zzz")
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
                    }
                }
            }
        }
        .listStyle(.insetGrouped)
        .navigationTitle("Oura days")
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

    /// Readiness and sleep as one charted series set, dropping the zeroes.
    ///
    /// A zero score is a row stored without that panel rather than a terrible night — Oura does
    /// not issue a readiness of nought — which is the same rule `TodaySnapshot.latestReadiness`
    /// applies, and for the same reason.
    static func scorePoints(in days: [OuraDay]) -> [ScorePoint] {
        days.flatMap { day -> [ScorePoint] in
            var points: [ScorePoint] = []
            if let readiness = day.readiness, readiness.score > 0 {
                points.append(ScorePoint(day: day.date, measure: "Readiness", score: readiness.score))
            }
            if let sleep = day.sleep, sleep.score > 0 {
                points.append(ScorePoint(day: day.date, measure: "Sleep", score: sleep.score))
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
                Text("Stored with no scores on it")
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

    private var scores: [String] {
        OuraPanel.panels(of: day).compactMap { panel in
            guard let score = panel.score, score > 0 else { return nil }
            return "\(panel.title) \(score)"
        }
    }
}
