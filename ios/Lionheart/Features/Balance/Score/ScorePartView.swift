import SwiftUI

/// One part of the training score opened: its points this week and over the weeks before, every
/// measurement against its target with what it earned, and what lies behind them — the patterns
/// for lifting, the sessions counted and declined for cardio, the lifts for progress.
///
/// Reads the score through the environment rather than taking it, so a refresh while it is open
/// shows the refreshed week; Train and Today both open it.
struct ScorePartView: View {
    let part: ScorePart

    @Environment(TrainingBalanceStore.self) private var balance
    @Environment(CardioStore.self) private var cardio
    @State private var isInfoShown = false
    @ScaledMetric(relativeTo: .body) private var trendHeight: CGFloat = 120

    var body: some View {
        List {
            switch balance.score {
            case .loading:
                Section { SkeletonRows(count: 4) }
            case .unavailable(let failure):
                ContentUnavailableView {
                    Text("Score not available")
                } description: {
                    Text(failure.message)
                }
                .listRowBackground(Color.clear)
            case .loaded(let score):
                content(score)
            case .stale(let score, let staleness):
                StaleBanner(staleness)
                content(score)
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle(part.name)
        .navigationBarTitleDisplayMode(.inline)
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button("How \(part.name.lowercased()) is scored", systemImage: "info.circle") { isInfoShown = true }
            }
        }
        .sheet(isPresented: $isInfoShown) {
            if let score = balance.score.value {
                ScoreInfoSheet(part: part, score: score, maxHeartRate: windowReport?.maxHeartRate)
            }
        }
        .refreshable {
            async let score: Void = balance.refresh()
            async let window: Void = loadWindow()
            _ = await (score, window)
        }
        .task {
            if part == .cardio, windowReport == nil { await loadWindow() }
        }
    }

    private var windowReport: CardioReport? {
        balance.score.value.flatMap { cardio.windows[$0.window.to]?.value }
    }

    private func loadWindow() async {
        guard part == .cardio, let window = balance.score.value?.window else { return }
        await cardio.load(window: window)
    }

    @ViewBuilder
    private func content(_ score: TrainingScore) -> some View {
        if let breakdown = ScoreBreakdown.of(score).first(where: { $0.part == part }) {
            headline(breakdown, score: score)
            Section {
                ForEach(breakdown.cells) { ScoreCellRow(cell: $0) }
            } header: {
                Text("What it earned")
            } footer: {
                Text(footer(score))
            }
            switch part {
            case .lifting: liftingDetail
            case .cardio: cardioDetail(score)
            case .progress: progressDetail(score.progress)
            case .consistency, .unrecognised: EmptyView()
            }
        }
    }

    private func headline(_ breakdown: ScorePartBreakdown, score: TrainingScore) -> some View {
        Section {
            HStack(alignment: .firstTextBaseline, spacing: 6) {
                Text(breakdown.points.map(ScorePoints.text) ?? "Unknown")
                    .font(.largeTitle.weight(.bold))
                    .monospacedDigit()
                Text("of \(ScorePoints.text(breakdown.outOf)) points")
                    .foregroundStyle(Color(.secondaryLabel))
            }
            .accessibilityElement(children: .combine)
            if let weeks = balance.weeks.value, weeks.count > 1 {
                WeeklyScoreChart(weeks: weeks, highlighted: weeks.last?.window.to, part: part, outOf: breakdown.outOf)
                    .frame(height: trendHeight)
                    .padding(.vertical, 4)
            }
        } footer: {
            Text("Seven days to \(Formatting.dayInline(score.window.to)). The bars are the same part in each week to its Sunday, the last in colour.")
        }
    }

    private func footer(_ score: TrainingScore) -> String {
        switch part {
        case .lifting:
            "Each family pays a third. Over the target in one does not make up for another."
        case .cardio:
            "Minutes inside cardio sessions, against the week's targets. Each pays the square root of its share of the target."
        case .progress:
            "A new best pays a family's third in full; held or first back a quarter; below a tenth."
        case .consistency:
            "Each block pays a quarter, against \(score.consistency.target) sessions."
        case .unrecognised:
            ""
        }
    }

    // MARK: - Behind each part

    @ViewBuilder
    private var liftingDetail: some View {
        if let volume = balance.volume.value {
            Section {
                NavigationLink {
                    VolumeView(volume: volume)
                } label: {
                    LabeledContent("Patterns and muscle groups") {
                        if !volume.unplaced.isEmpty {
                            Text("\(volume.unplaced.count) not counted").monospacedDigit()
                        }
                    }
                }
            }
        }
    }

    @ViewBuilder
    private func cardioDetail(_ score: TrainingScore) -> some View {
        if case .measured(_, _, _, _, let sessions, let heartRateDays) = score.cardio.measure {
            Section {
                LabeledContent("Cardio sessions") { Text("\(sessions)").monospacedDigit() }
                LabeledContent("Days with heart rate") { Text("\(heartRateDays) of 7").monospacedDigit() }
            }
        } else if case .unknown(let reason) = score.cardio.measure {
            Section {
                Text(CardioWords.unknown(reason)).foregroundStyle(Color(.secondaryLabel))
            }
        }
        switch cardio.windows[score.window.to] ?? .loading {
        case .loading:
            Section("Sessions") { SkeletonRows(count: 2) }
        case .unavailable(let failure):
            Section("Sessions") {
                Text(failure.message).font(.footnote).foregroundStyle(Color(.secondaryLabel))
            }
        case .loaded(let report), .stale(let report, _):
            CardioWindowSections(report: report)
        }
    }

    private func progressDetail(_ progress: ProgressPart) -> some View {
        Section {
            ForEach(progress.families, id: \.of.id) { family in
                NavigationLink(value: HistoryRoute.focus) {
                    PriorityProgressRow(family: family.of.family, lift: family.of.lift)
                }
            }
        } header: {
            Text("The priority lifts")
        }
    }
}
