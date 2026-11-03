import SwiftUI

/// The warm-up ramp to a working weight, offered and never forced.
///
/// Each step can be logged on its own as the athlete does it, or all at once afterwards; either way
/// they land as warm-ups, which set no record and count toward no total. Nothing here starts a rest.
struct WarmUpSheet: View {
    let title: String
    let ramp: WarmUpRamp
    let working: Double
    /// The plates to show per step, on a barbell.
    let plates: PlateSet?
    let log: (WarmUpRamp.Step) -> Void

    @Environment(\.dismiss) private var dismiss
    /// The steps logged from this sheet, so a step is not logged twice and the rest can be.
    @State private var logged: Set<String> = []

    var body: some View {
        NavigationStack {
            List {
                if ramp.isEmpty {
                    ContentUnavailableView {
                        Text("No warm-up")
                    } description: {
                        Text("\(Formatting.weight(working, ramp.unit)) is light enough to start on.")
                    }
                    .listRowBackground(Color.clear)
                } else {
                    Section {
                        ForEach(ramp.steps) { step in row(step) }
                    } header: {
                        Text("To \(Formatting.weight(working, ramp.unit))")
                    } footer: {
                        Text("Warm-ups set no records and count toward no totals.")
                    }
                }
            }
            .listStyle(.insetGrouped)
            .navigationTitle(title)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Done") { dismiss() }
                }
                ToolbarItem(placement: .topBarTrailing) {
                    Button("Log all") {
                        ramp.steps.filter { !logged.contains($0.id) }.forEach(log)
                        dismiss()
                    }
                    .fontWeight(.semibold)
                    .disabled(ramp.isEmpty || ramp.steps.allSatisfy { logged.contains($0.id) })
                }
            }
        }
        .presentationDetents([.medium, .large])
    }

    private func row(_ step: WarmUpRamp.Step) -> some View {
        let isLogged = logged.contains(step.id)
        return HStack(alignment: .firstTextBaseline, spacing: Tokens.cardPadding) {
            VStack(alignment: .leading, spacing: 2) {
                Text("\(Formatting.weight(step.weight, ramp.unit)) × \(step.reps)")
                    .font(.body.monospacedDigit())
                if let plates {
                    Text(Formatting.plates(PlateLoading.of(step.weight, on: plates), unit: ramp.unit))
                        .font(.footnote.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
            Spacer(minLength: 0)
            Button(isLogged ? "Logged" : "Log") {
                log(step)
                logged.insert(step.id)
            }
            .buttonStyle(.bordered)
            .disabled(isLogged)
        }
        .accessibilityElement(children: .combine)
    }
}
