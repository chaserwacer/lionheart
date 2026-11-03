import SwiftUI

/// Every session of the movement, newest first: the day and the session's best single, then each
/// set, with the ones that set a record marked.
struct MovementSessionsSections: View {
    let movement: MovementHistory
    let marks: RecordMarks
    let today: CalendarDay

    var body: some View {
        ForEach(movement.sessions) { session in
            let numbers = SetKind.numbers(of: session.sets.map(\.entry.kind))
            Section {
                ForEach(Array(session.sets.enumerated()), id: \.element.id) { position, set in
                    SetRow(
                        number: numbers[position],
                        entry: set.entry,
                        record: marks.outcome(of: .init(sessionId: session.sessionId, order: set.order)))
                }
            } header: {
                NavigationLink(value: HistoryRoute.session(session.sessionId)) {
                    HeaderLinkLabel(Text(Formatting.date(session.day, today: today))) {
                        if let estimate = estimate(of: session) {
                            Text(estimate).monospacedDigit()
                        }
                    }
                }
                .accessibilityHint("Opens the session")
            } footer: {
                if marks.setSessionVolume(in: session.sessionId, on: .movement(movement.movementDataId)) {
                    Text("Best session volume.")
                }
            }
        }
    }

    /// The session's best estimated single, in the unit the movement is lifted in now.
    private func estimate(of session: MovementHistory.Session) -> String? {
        guard let unit = movement.units.first,
              let best = MovementHistory.best(.estimatedOneRepMax, of: session.lifts(in: unit))
        else { return nil }
        return "e1RM \(Formatting.estimateFigure(best.value))"
    }
}
