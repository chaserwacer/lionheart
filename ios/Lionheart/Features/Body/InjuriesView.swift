import SwiftUI

/// Every injury on record, active first.
///
/// Resolved injuries are kept and shown, in their own section: an injury that healed is history the
/// model reads against a new complaint, so hiding it would be throwing away the context that makes
/// the next one legible.
struct InjuriesView: View {
    let injuries: [Injury]
    let store: BodyStore

    @State private var isNewInjuryPresented = false

    var body: some View {
        List {
            if injuries.isEmpty {
                ContentUnavailableView {
                    Text("No injuries tracked")
                } description: {
                    Text("Tracking one keeps its events — treatments and check-ins — in one place Lionheart can read.")
                }
                .listRowBackground(Color.clear)
            } else {
                section("Active", injuries.filter(\.isActive))
                section("Resolved", injuries.filter { !$0.isActive })
            }
        }
        .listStyle(.insetGrouped)
        .listSectionSpacing(.compact)
        .navigationTitle("Injuries")
        .toolbar {
            ToolbarItem(placement: .topBarTrailing) {
                Button("New injury", systemImage: "plus") { isNewInjuryPresented = true }
            }
        }
        .sheet(isPresented: $isNewInjuryPresented) {
            NewInjurySheet(store: store)
        }
        .refreshable { await store.refresh() }
    }

    @ViewBuilder
    private func section(_ title: String, _ shown: [Injury]) -> some View {
        if !shown.isEmpty {
            Section(title) {
                ForEach(shown) { injury in
                    let history = InjuryHistory.of(injury)
                    NavigationLink(value: BodyRoute.injury(injury.injuryId)) {
                        InjuryRow(history: history, today: today)
                    }
                    .injuryActions(on: history, store: store)
                }
            }
        }
    }

    private var today: CalendarDay { CalendarDay(Date()) }
}
