import SwiftUI

/// Marking the days the athlete will not train: a calendar to tap a day on or off, a weekday to mark
/// every week, and the statements marked, to remove.
///
/// The stock multi-date calendar shows the days the server reads as non-training as selected, so a
/// tap is one change: a new day is marked once with the note above it, and a marked day is freed.
/// Freeing a day a weekly statement covers frees every day it covers, so that one is asked first.
struct NonTrainingDaysSheet: View {
    @Environment(NonTrainingDaysStore.self) private var store
    @Environment(\.dismiss) private var dismiss

    /// The note a tapped day is marked with — "travel" once, then tap the days.
    @State private var note = ""
    @State private var weekly = WeeklyChoice()
    @State private var unmarkingWeekly: Unmarking?

    /// A weekly statement waiting on the athlete's word before every day it covers is freed.
    private struct Unmarking: Identifiable {
        let day: CalendarDay
        let nonTrainingDayId: UUID

        var id: UUID { nonTrainingDayId }
    }

    /// The weekly statement being composed: its first day, and its last when it has one.
    private struct WeeklyChoice {
        var from = Date()
        var hasEnd = false
        var until = Date().addingTimeInterval(8 * 7 * 86_400)
        var note = ""
    }

    var body: some View {
        NavigationStack {
            Form {
                calendarSection
                weeklySection
                markedSection
            }
            .navigationTitle("Non-training days")
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Done") { dismiss() }
                }
            }
            .task { await store.refresh() }
            .confirmationDialog(
                "Free every \(unmarkingWeekly.map { ExpectationWords.weekday($0.day) } ?? "day")?",
                isPresented: Binding(get: { unmarkingWeekly != nil }, set: { if !$0 { unmarkingWeekly = nil } }),
                titleVisibility: .visible,
                presenting: unmarkingWeekly
            ) { unmarking in
                Button("Remove the weekly mark", role: .destructive) {
                    Task { await store.remove(unmarking.nonTrainingDayId) }
                }
            } message: { _ in
                Text("This day is marked off every week. Removing it frees every week's, past and to come.")
            }
            .alert(
                "Refused",
                isPresented: Binding(get: { store.action.refusal != nil }, set: { if !$0 { store.dismissRefusal() } })
            ) {
                Button("OK") { store.dismissRefusal() }
            } message: {
                if let refusal = store.action.refusal { Text(refusal.message) }
            }
        }
    }

    // MARK: - The calendar

    @ViewBuilder
    private var calendarSection: some View {
        Section {
            TextField("Note for the days you tap, like travel", text: $note)
            switch store.calendar {
            case .loading:
                SkeletonRows(count: 3)
            case .unavailable(let failure):
                Text(failure.message).font(.footnote).foregroundStyle(Color(.secondaryLabel))
            case .loaded(let calendar), .stale(let calendar, _):
                MultiDatePicker("Non-training days", selection: selection(in: calendar), in: dates)
                    .disabled(store.action.isRunning)
            }
        } footer: {
            Text("Tap a day to mark it non-training, tap it again to free it. The week's targets are prorated to the days left. A planned session counts its day as a training day over a weekly mark.")
        }
    }

    /// The selected days are the server's non-training days; a change to them is read as one tap.
    private func selection(in calendar: AvailabilityCalendar) -> Binding<Set<DateComponents>> {
        let before = NonTrainingMarking.marked(in: calendar)
        return Binding(
            get: { Set(before.map(Self.components)) },
            set: { picked in
                let after = Set(picked.compactMap(Self.day))
                switch NonTrainingMarking.change(from: before, to: after, in: calendar) {
                case .mark(let day)?:
                    Task { await store.mark(.once(day, note: note)) }
                case .unmark(let day, let id, true)?:
                    unmarkingWeekly = Unmarking(day: day, nonTrainingDayId: id)
                case .unmark(_, let id, false)?:
                    Task { await store.remove(id) }
                case nil:
                    break
                }
            })
    }

    private var dates: Range<Date> {
        let range = store.range
        return range.from.startDate() ..< range.to.adding(days: 1).startDate()
    }

    private static func components(_ day: CalendarDay) -> DateComponents {
        Calendar.current.dateComponents([.calendar, .era, .year, .month, .day], from: day.middayDate())
    }

    private static func day(_ components: DateComponents) -> CalendarDay? {
        guard let year = components.year, let month = components.month, let day = components.day else { return nil }
        return CalendarDay(year: year, month: month, day: day)
    }

    // MARK: - Every week

    private var weeklySection: some View {
        Section {
            DatePicker("Starting", selection: $weekly.from, displayedComponents: .date)
            Toggle("Until a day", isOn: $weekly.hasEnd)
            if weekly.hasEnd {
                DatePicker("Until", selection: $weekly.until, in: weekly.from..., displayedComponents: .date)
            }
            TextField("Note, like family day", text: $weekly.note)
            Button("Mark every \(ExpectationWords.weekday(CalendarDay(weekly.from)))") {
                let draft = NonTrainingDayDraft(
                    day: CalendarDay(weekly.from),
                    repeatsWeekly: true,
                    until: weekly.hasEnd ? CalendarDay(weekly.until) : nil,
                    note: weekly.note)
                Task {
                    await store.mark(draft)
                    if store.action.refusal == nil { weekly = WeeklyChoice() }
                }
            }
            .disabled(store.action.isRunning)
        } header: {
            Text("Every week")
        } footer: {
            Text("From the starting day on, so the weeks already past keep their days.")
        }
    }

    // MARK: - Marked

    @ViewBuilder
    private var markedSection: some View {
        if let marked = store.marked.value, !marked.isEmpty {
            Section("Marked") {
                ForEach(marked) { day in
                    VStack(alignment: .leading, spacing: 2) {
                        Text(ExpectationWords.schedule(day.schedule))
                        if !day.note.isEmpty {
                            Text(day.note)
                                .font(.caption)
                                .foregroundStyle(Color(.secondaryLabel))
                        }
                    }
                    .swipeActions {
                        Button("Remove", role: .destructive) {
                            Task { await store.remove(day.nonTrainingDayId) }
                        }
                    }
                }
            }
        }
    }
}
