import SwiftUI

/// Every injury on record, active first.
///
/// Resolved injuries are kept and shown, in their own section: an injury that healed is history the
/// coach reads against a new complaint, so hiding it would be throwing away the context that makes
/// the next one legible.
struct InjuriesView: View {
    let injuries: [Injury]

    var body: some View {
        List {
            if injuries.isEmpty {
                ContentUnavailableView {
                    Label("No injuries tracked", systemImage: "bandage")
                } description: {
                    Text("Tracking one keeps its events — treatments and check-ins — in one place the coach can read.")
                }
                .listRowBackground(Color.clear)
            } else {
                section("Active", injuries.filter(\.isActive))
                section("Resolved", injuries.filter { !$0.isActive })
            }
        }
        .listStyle(.insetGrouped)
        .navigationTitle("Injuries")
    }

    @ViewBuilder
    private func section(_ title: String, _ shown: [Injury]) -> some View {
        if !shown.isEmpty {
            Section(title) {
                ForEach(shown) { injury in
                    NavigationLink(value: BodyRoute.injury(injury.injuryId)) {
                        InjuryRow(history: InjuryHistory.of(injury), today: today)
                    }
                }
            }
        }
    }

    private var today: CalendarDay { CalendarDay(Date()) }
}
