import SwiftUI

/// A pin read in full, as labelled rows: horizon, sessions against the frequency, the estimated
/// single before and since, the target, and the last session.
struct FocusReadingRows: View {
    let reading: FocusReading
    /// Off on a movement's own page, whose summary already says when it was last done.
    var showsLastDone = true

    var body: some View {
        let attendance = reading.attendance

        LabeledContent("Horizon") {
            Text("\(FocusWords.horizon(reading.pin.horizon)) · \(FocusWords.phase(reading.phase))")
                .multilineTextAlignment(.trailing)
        }
        LabeledContent {
            Text(FocusWords.attendance(attendance))
                .monospacedDigit()
                .multilineTextAlignment(.trailing)
        } label: {
            VStack(alignment: .leading, spacing: 1) {
                Text("Sessions")
                if let perWeek = attendance.perWeek {
                    Text("\(Formatting.rate(perWeek)) a week against \(attendance.sessionsPerWeek)")
                        .font(.caption.monospacedDigit())
                        .foregroundStyle(Color(.secondaryLabel))
                }
            }
        }
        if let progress = reading.progress {
            LabeledContent("Estimated 1RM") {
                Text(FocusWords.progress(progress))
                    .monospacedDigit()
                    .multilineTextAlignment(.trailing)
            }
        }
        if let target = FocusWords.target(reading.target) {
            LabeledContent("Target") {
                Text(target)
                    .monospacedDigit()
                    .multilineTextAlignment(.trailing)
            }
        }
        if showsLastDone {
            LabeledContent("Last done") {
                Text(reading.lastDone.map { Formatting.day(RelativeDay.of($0, today: CalendarDay(Date()))) } ?? "Never")
            }
        }
    }
}
