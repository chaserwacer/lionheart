import SwiftUI

/// Oura's readiness score, as a ring.
///
/// Built on the stock `Gauge` with `.accessoryCircularCapacity`, which is Apple's own ring: it gets
/// Dynamic Type, VoiceOver and both appearances right, and it is what Fitness and the Watch use for
/// the same job. The only thing added is the band colour, because the score's meaning is Oura's
/// banding and a single-tint ring would not carry it.
struct ReadinessGauge: View {
    let reading: ReadinessReading

    var body: some View {
        Gauge(value: Double(reading.score), in: 0...100) {
            Text("Readiness")
        } currentValueLabel: {
            Text("\(reading.score)")
                .font(.title3.bold().monospacedDigit())
        }
        .gaugeStyle(.accessoryCircularCapacity)
        .tint(bandColour)
        .accessibilityLabel("Readiness")
        .accessibilityValue("\(reading.score) out of 100, \(reading.band.label)")
    }

    /// The semantic system colours, so a band reads the same way it does everywhere else in iOS.
    private var bandColour: Color {
        switch reading.band {
        case .payAttention: .orange
        case .good: .yellow
        case .optimal: .green
        }
    }
}
