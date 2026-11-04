import SwiftUI

/// What each Apple Health figure is, how the device arrives at it, and what moves it — opened from
/// the gait section, the watch's history and a figure's own screen.
struct MeasureInfoSheet: View {
    let metrics: [HealthMetric]
    let title: String

    @Environment(\.dismiss) private var dismiss

    var body: some View {
        NavigationStack {
            List {
                Section {
                    ForEach(metrics, id: \.self) { metric in
                        VStack(alignment: .leading, spacing: 2) {
                            Text("\(metric.title), \(metric.unit)").font(.subheadline.weight(.semibold))
                            Text(Self.meaning(of: metric)).font(.subheadline).foregroundStyle(Color(.secondaryLabel))
                        }
                        .accessibilityElement(children: .combine)
                    }
                }
                Section("How it is read") {
                    Text("Each day is the mean of that day's readings from one device. A figure's level is the mean of the last four weeks, set against the twelve weeks before them; the charts draw a week's mean as the line and its days as points.")
                    Text("Only what your Apple Watch or iPhone measured is kept. Another app's copy in Health is left out — Oura's HRV is RMSSD written under Health's SDNN type, so it is not the same figure.")
                }
            }
            .listStyle(.insetGrouped)
            .navigationTitle(title)
            .navigationBarTitleDisplayMode(.inline)
            .toolbar {
                ToolbarItem(placement: .confirmationAction) {
                    Button("Done") { dismiss() }
                }
            }
        }
    }

    static func meaning(of metric: HealthMetric) -> String {
        switch metric {
        case .walkingSpeed:
            "Your speed on level ground in walks the iPhone measures in a pocket or at the hip. It falls with pain, fatigue or a limp; a slide over months is lost capacity."
        case .walkingStepLength:
            "The distance from one foot strike to the next. It shortens on a stiff or sore leg, and with a cautious gait."
        case .walkingAsymmetry:
            "The share of steps whose timing differs between your left and right leg. A sound gait sits at a few percent at most; it climbs when you favour one leg — a knee, an ankle or a hip you are protecting."
        case .walkingDoubleSupport:
            "The share of each stride with both feet on the ground, typically 20 to 40%. It rises when you trust one leg less: after an injury, with pain, or when balance is off."
        case .walkingSteadiness:
            "Apple's estimate of how stable your walking is over the past weeks, drawn from the other gait figures. Below about 40% Apple calls it low."
        case .sixMinuteWalkDistance:
            "The phone's estimate of how far you would cover in a six-minute walk test, a clinical measure of walking capacity, from your walking speed and heart rate."
        case .vo2Max:
            "The watch's estimate of your aerobic ceiling from heart rate against pace on outdoor walks, runs and hikes. An estimate from a model, not a test: read the trend."
        case .restingHeartRate:
            "The watch's resting rate for the day, from the quietest stretches it measured while you were awake and still. Oura's is the night's lowest — a different method, so the two run at different levels."
        case .heartRateVariabilitySdnn:
            "SDNN: the spread of beat-to-beat intervals across the watch's short readings through the day and night. Oura's HRV is RMSSD across the night. They are different statistics and are never compared; set SDNN only against itself."
        case .respiratoryRate:
            "Breaths a minute while you sleep, measured by the watch. Steady from night to night; a rise of one or two is an early sign of illness or strain."
        case .walkingHeartRateAverage:
            "Your mean heart rate while walking through the day. Lower at the same pace is fitter; it rises with heat, illness and fatigue."
        }
    }
}
