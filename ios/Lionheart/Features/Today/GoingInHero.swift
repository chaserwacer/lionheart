import SwiftUI

/// Today's hero: how ready he is going in — Tempo's rating from readiness, the night's HRV and heart
/// rate, sleep, load and wellness — the five parts as leaning bars, and what is up next, on Graphite.
///
/// The server's going-in state, the one the logging surface opens with and the chat reasons from,
/// so the morning's verdict is the same everywhere. The band's colour follows the rating's rule —
/// green over his usual day, blue on it, orange and red under, never Brick — and only the part
/// holding the rating down is coloured, orange. A tap opens the parts and the method.
struct GoingInHero: View {
    let state: GoingInState
    let next: NextSessions?
    /// Whether the read is an older answer recalled with no signal, or a failed refresh.
    let isStale: Bool
    let openDetail: () -> Void

    var body: some View {
        let reading = GoingInReading.of(state, next: next, movements: [], hasWork: false)
        HeroSection {
            Button(action: openDetail) {
                summary(reading)
            }
            .buttonStyle(.plain)
            .accessibilityHint("Shows the parts and how they are weighed")
            if let next {
                NextUpLine(next: next)
            }
        }
    }

    private func summary(_ reading: GoingInReading) -> some View {
        VStack(alignment: .leading, spacing: 10) {
            HStack(alignment: .firstTextBaseline) {
                Text("Going in · \(state.asOf.formatted(date: .omitted, time: .shortened))")
                    .font(.meta)
                    .foregroundStyle(Color(.secondaryLabel))
                Spacer(minLength: 8)
                Image(systemName: "info.circle")
                    .foregroundStyle(.tint)
                    .accessibilityHidden(true)
            }
            HeroFigure(
                label: "Ready",
                figure: GoingInWords.score(reading.rating),
                unit: GoingInWords.band(reading.rating),
                value: reading.rating.score.map(Double.init),
                unitTint: GoingInWords.tint(reading.rating))
            Text(GoingInWords.verdict(reading))
                .font(.footnote)
                .foregroundStyle(Color(.secondaryLabel))
                .fixedSize(horizontal: false, vertical: true)
            GoingInPartsStrip(parts: reading.parts, weakest: reading.weakest?.kind)
            if isStale {
                Text("Last read, not current")
                    .font(.caption)
                    .foregroundStyle(.orange)
            }
        }
        .padding(.vertical, 4)
        .contentShape(.rect)
        .accessibilityElement(children: .ignore)
        .accessibilityLabel("Going in")
        .accessibilityValue(
            "\(GoingInWords.score(reading.rating)), \(GoingInWords.band(reading.rating)). \(GoingInWords.verdict(reading)) "
                + reading.parts.map(GoingInWords.spoken).joined(separator: ", "))
    }
}

/// The five parts across the row, each its score over a leaning bar as tall as the part, so the eye
/// finds the one pulling the rating down. They rise in turn as they appear, the opening's motion.
private struct GoingInPartsStrip: View {
    let parts: [GoingInReading.Part]
    let weakest: GoingInReading.Part.Kind?

    @ScaledMetric(relativeTo: .caption) private var barHeight: CGFloat = 34
    @Environment(\.accessibilityReduceMotion) private var reduceMotion
    @State private var hasRisen = false

    var body: some View {
        HStack(alignment: .bottom, spacing: 6) {
            ForEach(Array(parts.enumerated()), id: \.element.id) { index, part in
                VStack(spacing: 3) {
                    Text(part.score.score.map(String.init) ?? "–")
                        .font(.caption2.monospacedDigit())
                        .foregroundStyle(part.kind == weakest ? Color.orange : Color(.secondaryLabel))
                    // Upright, in the mark's proportions, rather than as wide as the column.
                    LeanedColumn(rise: hasRisen ? rise(of: part) : 0)
                        .fill(fill(of: part))
                        .frame(width: barHeight * 0.55, height: barHeight)
                        .animation(reduceMotion ? nil : .brandRise(step: index), value: hasRisen)
                    Text(GoingInWords.shortName(part.kind))
                        .font(.caption2)
                        .foregroundStyle(Color(.secondaryLabel))
                        .lineLimit(1)
                        .minimumScaleFactor(0.5)
                }
                .frame(maxWidth: .infinity)
            }
        }
        .onAppear { hasRisen = true }
    }

    /// Each part out of 100, with a stub for an unread part so its slot still shows.
    private func rise(of part: GoingInReading.Part) -> Double {
        part.score.score.map { max(0.06, min(1, Double($0) / 100)) } ?? 0.06
    }

    private func fill(of part: GoingInReading.Part) -> Color {
        if part.score.score == nil { return Color(.tertiarySystemFill) }
        return part.kind == weakest ? .orange : Tokens.ink
    }
}
