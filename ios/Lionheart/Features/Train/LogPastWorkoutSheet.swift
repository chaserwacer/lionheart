import SwiftUI

/// Starting a workout that already happened.
///
/// All it asks for is when it began, because the server reads the session's calendar day off the
/// start — everything else is logged on the same surface as a live workout, and the length is
/// settled when the athlete finishes. Reusing that surface is deliberate: logging after the fact is
/// the common case, and it deserves the good screen rather than a lesser form of its own.
///
/// Nothing is sent here, or while the sets are being entered. `POST /completed` carries the whole
/// session at the end, which is what lets this path work having never had a connection.
struct LogPastWorkoutSheet: View {
    let store: TrainStore
    let openWorkout: () -> Void

    @Environment(\.dismiss) private var dismiss

    /// Yesterday evening, which is the most likely answer by a wide margin: a session gets written
    /// down the morning after it was trained.
    @State private var startedAt = Self.yesterdayEvening()

    private static func yesterdayEvening(now: Date = Date(), calendar: Calendar = .current) -> Date {
        let yesterday = calendar.date(byAdding: .day, value: -1, to: now) ?? now
        return calendar.date(bySettingHour: 17, minute: 30, second: 0, of: yesterday) ?? yesterday
    }

    var body: some View {
        NavigationStack {
            Form {
                Section {
                    DatePicker("Started", selection: $startedAt, in: ...Date())
                } header: {
                    Text("When did it start?")
                } footer: {
                    Text("The session is recorded on this day. You can change when it finished at the end.")
                }

                Section {
                    Button {
                        if store.beginAfterTheFact(startedAt: startedAt) {
                            openWorkout()
                            dismiss()
                        }
                    } label: {
                        Text("Log the sets")
                            .fontWeight(.semibold)
                    }
                    .disabled(store.isWorkoutInFlight)
                } footer: {
                    if store.isWorkoutInFlight {
                        Text("Finish the workout you are logging first.")
                    }
                }
            }
            .navigationTitle("Past workout")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .topBarLeading) {
                    Button("Cancel") { dismiss() }
                }
            }
            .presentationDetents([.medium, .large])
        }
    }
}
