import SwiftUI

/// How consistently training happens, as measurement: sessions per week over a span, the runs of
/// weeks that held a session, and a calendar of every training day back to the first.
///
/// Plain counts and dates, deliberately. The athlete's first goal is consistency and his own
/// analysis sets the terms — weeks that happen at all, and the 35-week stretch that simply did not
/// stop — so the screen states those and nothing else: no streak to keep alive, no colour for a
/// week missed. A day with training is marked; a day without is just a day.
struct ConsistencyView: View {
    let history: HistoryStore
    let diary: DiaryStore
    let library: MovementLibraryStore

    @State private var span: LogSpan = .half
    @State private var openedDay: CalendarDay?

    @ScaledMetric(relativeTo: .body) private var chartHeight: CGFloat = 170

    var body: some View {
        let today = CalendarDay(Date())

        List {
            if let snapshot = history.state.value {
                content(snapshot, today: today)
            } else {
                Section { SkeletonRows(count: 3) }
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Consistency")
        .navigationBarTitleDisplayMode(.inline)
        .task { await history.refreshIfNeeded() }
        .navigationDestination(item: $openedDay) { day in
            DayView(day: day, history: history, diary: diary, library: library)
        }
    }

    @ViewBuilder
    private func content(_ snapshot: HistorySnapshot, today: CalendarDay) -> some View {
        let reading = snapshot.consistency

        if reading.weeks.isEmpty {
            ContentUnavailableView {
                Text("No sessions logged")
            } description: {
                Text("Training days and sessions per week are read from completed sessions.")
            }
            .listRowBackground(Color.clear)
        } else {
            Section {
                Picker("Span", selection: $span) {
                    ForEach(LogSpan.allCases) { Text($0.label).tag($0) }
                }
                .pickerStyle(.segmented)
                .listRowBackground(Color.clear)
                .listRowInsets(EdgeInsets())
            }
            weeklySection(reading)
            runsSection(reading)
            calendar(reading: reading, today: today)
        }
    }

    private func weeklySection(_ reading: ConsistencyReading) -> some View {
        let window = reading.window(lastWeeks: span.weeks(of: reading.weeks.count))

        return Section {
            SummaryFigures(figures: [
                ("Sessions", "\(window.sessions)"),
                ("Per week", Formatting.rate(window.perWeek)),
                ("Weeks trained", "\(window.weeksTrained) of \(window.weeks.count)"),
            ])
            if let median = window.medianInTrainedWeeks {
                LabeledContent("In a week trained, median") {
                    Text(Formatting.count(Int(median.rounded()), "session")).monospacedDigit()
                }
            }
            WeeklyLoadChart(
                weeks: window.weeks,
                average: window.average,
                counting: .sessions)
                .frame(height: chartHeight)
                .padding(.vertical, 6)
        } header: {
            Text("Sessions per week")
        } footer: {
            Text("Bars are weeks, Monday to Sunday, empty ones included; the line is the four-week average.")
        }
    }

    /// The runs of weeks that each held a session: the one still going, and the longest.
    private func runsSection(_ reading: ConsistencyReading) -> some View {
        Section {
            LabeledContent("Current") {
                Text(reading.currentRun.map(describe) ?? "No session last week or this")
                    .monospacedDigit()
                    .multilineTextAlignment(.trailing)
            }
            if let longest = reading.longestRun {
                LabeledContent("Longest") {
                    Text(describe(longest))
                        .monospacedDigit()
                        .multilineTextAlignment(.trailing)
                }
            }
        } header: {
            Text("Consecutive weeks with a session")
        } footer: {
            Text("A week counts with one session in it. This week counts once it has one.")
        }
    }

    /// A run's length and the days it spans, Monday of its first week to Sunday of its last, the
    /// year said once when both ends share it.
    private func describe(_ run: ConsistencyReading.WeekRun) -> String {
        let first = run.firstWeek
        let last = run.lastWeek.adding(days: 6)
        let day = Date.FormatStyle.dateTime.month(.abbreviated).day()
        let from = first.year == last.year
            ? first.middayDate().formatted(day)
            : first.middayDate().formatted(day.year())
        return "\(Formatting.count(run.weekCount, "week")) · \(from) – \(last.middayDate().formatted(day.year()))"
    }

    // MARK: - Calendar

    @ViewBuilder
    private func calendar(reading: ConsistencyReading, today: CalendarDay) -> some View {
        let first = reading.weeks.first?.start ?? today
        ForEach(CalendarMonth.months(from: first, through: today)) { month in
            let trained = reading.trainedDays(in: month)
            Section {
                MonthGrid(
                    month: month,
                    trainingDays: reading.trainingDays,
                    today: today,
                    open: { day in openedDay = day })
            } header: {
                HStack(alignment: .firstTextBaseline) {
                    Text(Formatting.month(month.start))
                    Spacer()
                    Text(Formatting.count(trained, "day")).monospacedDigit()
                }
            }
        }
    }
}

/// One month as a calendar page, Monday first, with the days that held training marked and
/// tappable.
///
/// A grid of plain buttons rather than links: inside one list row, links are restyled as a single
/// disclosure row and the whole row becomes one tap target.
private struct MonthGrid: View {
    let month: CalendarMonth
    let trainingDays: Set<CalendarDay>
    let today: CalendarDay
    let open: (CalendarDay) -> Void

    @ScaledMetric(relativeTo: .footnote) private var cell: CGFloat = 28

    private static let weekdays = ["M", "T", "W", "T", "F", "S", "S"]

    var body: some View {
        Grid(horizontalSpacing: 0, verticalSpacing: 2) {
            GridRow {
                ForEach(Array(Self.weekdays.enumerated()), id: \.offset) { _, letter in
                    Text(letter)
                        .font(.caption2)
                        .foregroundStyle(Color(.tertiaryLabel))
                        .frame(maxWidth: .infinity)
                        .accessibilityHidden(true)
                }
            }
            ForEach(Array(month.weeks.enumerated()), id: \.offset) { _, week in
                GridRow {
                    ForEach(Array(week.enumerated()), id: \.offset) { _, day in
                        if let day {
                            dayCell(day)
                        } else {
                            Color.clear.frame(height: cell)
                        }
                    }
                }
            }
        }
        .padding(.vertical, 4)
    }

    @ViewBuilder
    private func dayCell(_ day: CalendarDay) -> some View {
        let trained = trainingDays.contains(day)
        let label = Text("\(day.day)")
            .font(.footnote.monospacedDigit().weight(trained ? .semibold : .regular))
            .frame(width: cell, height: cell)

        if trained {
            Button { open(day) } label: {
                label
                    .foregroundStyle(Color(.label))
                    .background(Tokens.accent.opacity(0.2), in: .circle)
            }
            .buttonStyle(.plain)
            .frame(maxWidth: .infinity)
            .accessibilityLabel("\(Formatting.date(day)), trained")
        } else if day <= today {
            // A day without a session still has a night, steps and maybe a ride, so it opens too.
            Button { open(day) } label: {
                label.foregroundStyle(Color(.secondaryLabel))
            }
            .buttonStyle(.plain)
            .frame(maxWidth: .infinity)
            .accessibilityLabel(Formatting.date(day))
        } else {
            label
                .foregroundStyle(Color(.quaternaryLabel))
                .frame(maxWidth: .infinity)
                .accessibilityLabel(Formatting.date(day))
        }
    }
}
