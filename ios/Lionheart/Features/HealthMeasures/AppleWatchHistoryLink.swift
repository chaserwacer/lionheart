import SwiftUI

/// Under one of Oura's recovery figures, the Apple Watch's own history of the same thing, labelled
/// as the watch's and opening onto its own chart.
///
/// Beside Oura's chart, never on it: the watch's resting rate is a daytime figure against the
/// ring's night-time lowest, and its HRV is SDNN against the ring's RMSSD. Shown only where the
/// watch measured that figure.
struct AppleWatchHistoryLink: View {
    let metric: RecoveryMetric

    @Environment(HealthMeasuresStore.self) private var store

    var body: some View {
        if let watch = Self.watchFigure(for: metric) {
            let days = store.history.days(watch, by: .appleWatch)
            if let first = days.first, let last = days.last {
                Section {
                    NavigationLink {
                        MeasureHistoryView(metric: watch)
                    } label: {
                        LabeledContent {
                            Text(watch.text(days.map(\.mean).reduce(0, +) / Double(days.count)))
                                .monospacedDigit()
                        } label: {
                            VStack(alignment: .leading, spacing: 2) {
                                Text("Apple Watch · \(watch.title)")
                                Text("\(Formatting.date(first.day)) to \(Formatting.date(last.day)) · mean")
                                    .font(.subheadline.monospacedDigit())
                                    .foregroundStyle(Color(.secondaryLabel))
                            }
                        }
                    }
                } footer: {
                    Text(Self.caveat(for: watch))
                }
            }
        }
    }

    static func watchFigure(for metric: RecoveryMetric) -> HealthMetric? {
        switch metric {
        case .hrv: .heartRateVariabilitySdnn
        case .lowestHeartRate: .restingHeartRate
        case .respiratoryRate: .respiratoryRate
        case .readiness, .sleepDuration, .sleepScore, .temperatureDeviation, .steps: nil
        }
    }

    private static func caveat(for figure: HealthMetric) -> String {
        switch figure {
        case .heartRateVariabilitySdnn: "SDNN from the watch's short readings, not Oura's overnight RMSSD: not comparable."
        case .restingHeartRate: "The watch's daytime resting rate, not the ring's night-time lowest: they run at different levels."
        case .respiratoryRate: "Breaths a minute in sleep, as the watch measured them."
        case .vo2Max, .walkingHeartRateAverage, .walkingSpeed, .walkingStepLength, .walkingDoubleSupport,
             .walkingAsymmetry, .walkingSteadiness, .sixMinuteWalkDistance:
            ""
        }
    }
}
