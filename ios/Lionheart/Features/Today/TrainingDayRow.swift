import SwiftUI

/// One session on a day, as the head of a log-book entry: when it started, or its state, and what it
/// came to, with its notes.
///
/// The movements are not drawn here. Today lists each as its own row under this one
/// (`MovementLineRow`), so a movement opens its own page while this row opens the session.
struct TrainingDayRow: View {
    let session: TrainingSession
    /// Shown above the session when the row is not about the day on screen — "Last session" on a
    /// day with nothing logged.
    var dayLabel: String?

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    var body: some View {
        let headingLayout = dynamicTypeSize.isAccessibilitySize
            ? AnyLayout(VStackLayout(alignment: .leading, spacing: 2))
            : AnyLayout(HStackLayout(alignment: .firstTextBaseline))

        VStack(alignment: .leading, spacing: 4) {
            headingLayout {
                Text(heading)
                    .font(.subheadline.weight(.semibold))
                if !dynamicTypeSize.isAccessibilitySize { Spacer(minLength: 8) }
                if let facts {
                    Text(facts)
                        .font(.subheadline.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }

            if let notes = session.envelope.notes.nonEmpty {
                Text(notes)
                    .font(.footnote)
                    .foregroundStyle(Color(.secondaryLabel))
                    .lineLimit(3)
            }
        }
        .padding(.vertical, 2)
        .accessibilityElement(children: .combine)
    }

    /// What leads the row: the day when the row is not about the day on screen, the time a
    /// completed session started, or the state of any other.
    private var heading: String {
        if let dayLabel { return dayLabel }
        if case .completed(let completed) = session, let start = completed.period.startTime {
            return start.formatted(date: .omitted, time: .shortened)
        }
        return SessionLog.kind(of: session, today: CalendarDay(Date())).label
    }

    private var facts: String? {
        Formatting.sessionFacts(SessionLog.facts(of: session))
    }
}

/// One movement of a session as its own row — its name and its log-book line — which opens the
/// movement's page.
struct MovementLineRow: View {
    let digest: MovementDigest

    @Environment(\.dynamicTypeSize) private var dynamicTypeSize

    var body: some View {
        let layout = dynamicTypeSize.isAccessibilitySize
            ? AnyLayout(VStackLayout(alignment: .leading, spacing: 0))
            : AnyLayout(HStackLayout(alignment: .firstTextBaseline, spacing: 12))

        layout {
            Text(digest.movementName)
                .font(.subheadline)
                .lineLimit(2)
                .frame(maxWidth: .infinity, alignment: .leading)
            Text(MovementLines.line(digest.line))
                .font(.subheadline.monospacedDigit())
                .foregroundStyle(Color(.secondaryLabel))
                .fixedSize(horizontal: !dynamicTypeSize.isAccessibilitySize, vertical: false)
        }
        .accessibilityElement(children: .combine)
    }
}
