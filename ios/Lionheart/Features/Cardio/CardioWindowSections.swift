import SwiftUI

/// A score window's cardio sessions: the ones counted, and the ring's guesses declined with the
/// heart rate that decided each, so the call can be checked.
struct CardioWindowSections: View {
    let report: CardioReport
    @Environment(CardioStore.self) private var cardio

    var body: some View {
        Section {
            if report.sessions.isEmpty {
                Text("None in these seven days.").foregroundStyle(Color(.secondaryLabel))
            }
            ForEach(report.sessions.reversed()) { session in
                link(to: session) { CardioSessionRow(session: session) }
            }
        } header: {
            Text("Sessions counted")
        }
        if !report.declined.isEmpty {
            Section {
                ForEach(report.declined.reversed()) { declined in
                    link(to: declined.session) { DeclinedWorkoutRow(declined: declined) }
                }
            } header: {
                Text("Left out by heart rate")
            } footer: {
                Text("Workouts the ring detected on its own. Each needed 20 minutes at zone 2 or above, or 10 at zone 3 or above, for at least half the time read.")
            }
        }
    }

    private func link(to session: CardioSession, @ViewBuilder label: () -> some View) -> some View {
        NavigationLink {
            CardioSessionView(session: session, maxHeartRate: report.maxHeartRate, store: cardio)
        } label: {
            label()
        }
    }
}
