import Foundation

/// How each Apple Health figure is named and written on screen.
///
/// Shown in the unit a reader thinks in rather than the stored one where they differ: step length in
/// centimetres, and the three shares as percentages, since "4.2%" reads and "0.042" does not.
extension HealthMetric {
    var title: String {
        switch self {
        case .vo2Max: "VO₂max"
        case .restingHeartRate: "Resting heart rate"
        case .heartRateVariabilitySdnn: "HRV (SDNN)"
        case .respiratoryRate: "Respiratory rate"
        case .walkingHeartRateAverage: "Walking heart rate"
        case .walkingSpeed: "Walking speed"
        case .walkingStepLength: "Step length"
        case .walkingDoubleSupport: "Double support"
        case .walkingAsymmetry: "Walking asymmetry"
        case .walkingSteadiness: "Walking steadiness"
        case .sixMinuteWalkDistance: "Six-minute walk"
        }
    }

    var unit: String {
        switch self {
        case .vo2Max: "ml/kg/min"
        case .restingHeartRate, .walkingHeartRateAverage: "bpm"
        case .heartRateVariabilitySdnn: "ms"
        case .respiratoryRate: "br/min"
        case .walkingSpeed: "m/s"
        case .walkingStepLength: "cm"
        case .walkingDoubleSupport, .walkingAsymmetry, .walkingSteadiness: "%"
        case .sixMinuteWalkDistance: "m"
        }
    }

    /// Which way of moving is the better one.
    var polarity: RecoveryMetric.Polarity {
        switch self {
        case .vo2Max, .heartRateVariabilitySdnn, .walkingSpeed, .walkingStepLength, .walkingSteadiness,
             .sixMinuteWalkDistance:
            .higherIsBetter
        case .restingHeartRate, .respiratoryRate, .walkingHeartRateAverage, .walkingDoubleSupport, .walkingAsymmetry:
            .lowerIsBetter
        }
    }

    /// A stored value in the unit shown.
    func shown(_ stored: Double) -> Double {
        switch self {
        case .walkingStepLength, .walkingDoubleSupport, .walkingAsymmetry, .walkingSteadiness: stored * 100
        case .vo2Max, .restingHeartRate, .heartRateVariabilitySdnn, .respiratoryRate, .walkingHeartRateAverage,
             .walkingSpeed, .sixMinuteWalkDistance:
            stored
        }
    }

    /// Decimal places the figure is worth: no more than the device measures.
    var fractionDigits: Int {
        switch self {
        case .walkingSpeed: 2
        case .vo2Max, .respiratoryRate, .walkingDoubleSupport, .walkingAsymmetry: 1
        case .restingHeartRate, .heartRateVariabilitySdnn, .walkingHeartRateAverage, .walkingStepLength,
             .walkingSteadiness, .sixMinuteWalkDistance:
            0
        }
    }

    /// "1.32", from a stored value.
    func number(_ stored: Double) -> String {
        shown(stored).formatted(.number.precision(.fractionLength(fractionDigits)))
    }

    /// "1.32 m/s", "4.2%".
    func text(_ stored: Double) -> String {
        unit == "%" ? "\(number(stored))%" : "\(number(stored)) \(unit)"
    }

    /// "+0.4", "−1.2": a change in the unit shown, signed.
    func change(_ storedChange: Double) -> String {
        let shown = shown(storedChange)
        let magnitude = abs(shown).formatted(.number.precision(.fractionLength(fractionDigits)))
        return shown < 0 ? "−\(magnitude)" : "+\(magnitude)"
    }

    /// Whether a change is in the better direction; `nil` for one too small to show.
    func isImprovement(_ storedChange: Double) -> Bool? {
        guard number(abs(storedChange)) != 0.formatted(.number.precision(.fractionLength(fractionDigits))) else { return nil }
        return switch polarity {
        case .higherIsBetter: storedChange > 0
        case .lowerIsBetter: storedChange < 0
        }
    }
}

extension MeasuringDevice {
    var name: String {
        switch self {
        case .appleWatch: "Apple Watch"
        case .iPhone: "iPhone"
        }
    }
}
