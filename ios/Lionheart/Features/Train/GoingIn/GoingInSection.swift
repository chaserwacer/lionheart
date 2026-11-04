import SwiftUI

/// The top of the logging surface: how ready the athlete came in, the five parts that say why, and
/// what is worth knowing before the first set.
///
/// In full until the first set of work, then one line: by then he knows how he came in, and every
/// line this takes is a set pushed down the screen. Either way a tap opens the parts and the method
/// (`GoingInSheet`), because a number with no way to see where it came from is a number to ignore.
struct GoingInSection: View {
    let reading: GoingInReading
    let library: MovementLibraryStore
    /// Whether the read is an older answer recalled with no signal, or a failed refresh.
    let isStale: Bool
    let openDetail: () -> Void

    var body: some View {
        Section {
            if reading.isExpanded {
                Button(action: openDetail) { summary }
                    .buttonStyle(.plain)
                partsGrid
                notes
            } else {
                Button(action: openDetail) { compact }
                    .buttonStyle(.plain)
                ForEach(reading.injuries.filter { !$0.touching.isEmpty }) { injury in
                    injuryRow(injury)
                }
            }
        } header: {
            HStack {
                Text("Going in")
                Spacer()
                Button("How this is rated", systemImage: "info.circle", action: openDetail)
                    .labelStyle(.iconOnly)
                    .textCase(nil)
            }
        }
    }

    // MARK: - The rating

    private var summary: some View {
        HStack(spacing: Tokens.cardPadding) {
            RatingDial(rating: reading.rating)
            VStack(alignment: .leading, spacing: 2) {
                Text(GoingInWords.band(reading.rating))
                    .font(.title3.weight(.semibold))
                    .foregroundStyle(Color(.label))
                Text(GoingInWords.verdict(reading))
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
                    .fixedSize(horizontal: false, vertical: true)
                if isStale {
                    Text("Last read, not current")
                        .font(.caption)
                        .foregroundStyle(.orange)
                }
            }
            Spacer(minLength: 0)
        }
        .contentShape(.rect)
        .accessibilityElement(children: .combine)
        .accessibilityHint("Shows the parts and how they are weighed")
    }

    private var compact: some View {
        HStack(spacing: 8) {
            Text(GoingInWords.score(reading.rating))
                .font(.headline.monospacedDigit())
                .foregroundStyle(GoingInWords.tint(reading.rating))
            Text(GoingInWords.band(reading.rating))
                .font(.subheadline)
            Spacer(minLength: 4)
            // The scored parts by name where they fit, by number alone where they do not, and not
            // at all before a figure would truncate: the line is a reminder, and a tap opens them.
            ViewThatFits(in: .horizontal) {
                partsLine { "\(GoingInWords.shortName($0.kind)) \($1)" }
                partsLine { "\($1)" }
                EmptyView()
            }
            .font(.caption.monospacedDigit())
            .foregroundStyle(Color(.secondaryLabel))
        }
        .contentShape(.rect)
        .accessibilityElement(children: .combine)
    }

    private func partsLine(_ text: (GoingInReading.Part, Int) -> String) -> some View {
        Text(reading.parts.compactMap { part in part.score.score.map { text(part, $0) } }.joined(separator: " · "))
            .lineLimit(1)
            .fixedSize()
    }

    /// The five parts across one row, each a score over a capacity bar, so the eye finds the one
    /// pulling the rating down. Stacked as rows where five columns no longer fit.
    private var partsGrid: some View {
        ViewThatFits(in: .horizontal) {
            HStack(alignment: .top, spacing: 10) {
                ForEach(reading.parts) { part in
                    PartColumn(part: part)
                }
            }
            VStack(alignment: .leading, spacing: 8) {
                ForEach(reading.parts) { part in
                    PartRow(part: part)
                }
            }
        }
        .padding(.vertical, 2)
    }

    // MARK: - Notes

    @ViewBuilder
    private var notes: some View {
        ForEach(reading.injuries) { injury in
            injuryRow(injury)
        }
        ForEach(Array(reading.plannedToday.enumerated()), id: \.offset) { _, movements in
            note("Planned today", movements.joined(separator: ", "))
        }
        if !reading.recentMuscles.isEmpty {
            note("Trained since yesterday", GoingInWords.muscles(reading.recentMuscles))
        }
        if let upNext = reading.upNext {
            upNextRows(upNext)
        }
    }

    private func injuryRow(_ injury: GoingInReading.Injury) -> some View {
        let touching = injury.touching.map { library.title(of: $0) }
        let pain = injury.note.lastPain.map { "pain \($0)/10" }
        let detail = ([pain] + [touching.isEmpty ? nil : "touches \(touching.joined(separator: ", "))"])
            .compactMap { $0 }
            .joined(separator: " · ")
        return VStack(alignment: .leading, spacing: 2) {
            Text(injury.note.name)
                .font(.subheadline.weight(.semibold))
                .foregroundStyle(touching.isEmpty ? Color(.label) : Color.orange)
            if !detail.isEmpty {
                Text(detail)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
        .accessibilityElement(children: .combine)
        .accessibilityLabel("Active injury, \(injury.note.name). \(detail)")
    }

    private func note(_ label: String, _ value: String) -> some View {
        VStack(alignment: .leading, spacing: 2) {
            Text(label)
                .font(.caption)
                .foregroundStyle(Color(.secondaryLabel))
            Text(value)
                .font(.subheadline)
        }
        .accessibilityElement(children: .combine)
    }

    @ViewBuilder
    private func upNextRows(_ upNext: GoingInReading.UpNextLine) -> some View {
        note("Up next", GoingInWords.upNext(upNext))
        ForEach(upNext.priorities) { priority in
            priorityRow(priority)
        }
    }

    /// A due family's priority lift: what to beat, and how it went last time.
    private func priorityRow(_ priority: GoingInReading.Priority) -> some View {
        let lastTime = library.previous[priority.priority.movementDataId].map { previous in
            "Last \(Formatting.dayInline(previous.date)): "
                + previous.workingSets.compactMap(Formatting.lastTime).joined(separator: ", ")
        }
        return VStack(alignment: .leading, spacing: 2) {
            HStack(alignment: .firstTextBaseline) {
                Text(priority.priority.name)
                    .font(.subheadline.weight(.semibold))
                Spacer(minLength: 8)
                Text(GoingInWords.aim(priority.priority.aim))
                    .font(.subheadline.monospacedDigit())
                    .foregroundStyle(Tokens.accent)
            }
            if let lastTime {
                Text(lastTime)
                    .font(.footnote.monospacedDigit())
                    .foregroundStyle(Color(.secondaryLabel))
                    .lineLimit(2)
            }
            if priority.isInWorkout {
                Text("In this workout")
                    .font(.caption)
                    .foregroundStyle(Color(.secondaryLabel))
            }
        }
        .accessibilityElement(children: .combine)
    }
}

/// The rating as a capacity ring with its number inside, tinted by band.
private struct RatingDial: View {
    let rating: GoingInRating
    @ScaledMetric(relativeTo: .title) private var size: CGFloat = 56

    var body: some View {
        Gauge(value: Double(rating.score ?? 0), in: 0...100) {
            EmptyView()
        } currentValueLabel: {
            Text(GoingInWords.score(rating))
                .font(.title3.monospacedDigit().weight(.bold))
        }
        .gaugeStyle(.accessoryCircularCapacity)
        .tint(GoingInWords.tint(rating))
        .frame(width: size, height: size)
        .accessibilityLabel("Going-in rating \(GoingInWords.score(rating))")
    }
}

private struct PartColumn: View {
    let part: GoingInReading.Part

    var body: some View {
        VStack(alignment: .leading, spacing: 3) {
            Text(GoingInWords.shortName(part.kind))
                .font(.caption2)
                .foregroundStyle(Color(.secondaryLabel))
                .lineLimit(1)
            Text(part.score.score.map(String.init) ?? "–")
                .font(.subheadline.monospacedDigit().weight(.semibold))
                .foregroundStyle(part.score.score == nil ? Color(.tertiaryLabel) : Color(.label))
            Gauge(value: Double(part.score.score ?? 0), in: 0...100) { EmptyView() }
                .gaugeStyle(.accessoryLinearCapacity)
                .tint(GoingInWords.tint(score: part.score.score))
                .opacity(part.score.score == nil ? 0.3 : 1)
        }
        .frame(maxWidth: .infinity, alignment: .leading)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(GoingInWords.spoken(part))
    }
}

private struct PartRow: View {
    let part: GoingInReading.Part

    var body: some View {
        HStack {
            Text(GoingInWords.name(part.kind))
            Spacer()
            Text(part.score.score.map(String.init) ?? "Not read")
                .monospacedDigit()
                .foregroundStyle(part.score.score == nil ? Color(.secondaryLabel) : Color(.label))
        }
        .font(.subheadline)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel(GoingInWords.spoken(part))
    }
}
