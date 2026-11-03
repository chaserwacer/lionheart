import Foundation

/// The warm-up sets that build to a working weight, offered before the first working set.
///
/// A short ramp, because the working sets are two sets to failure and the warm-up's job is to get
/// there fresh: a few reps at falling volume and rising load, the last one close enough to the work
/// that the first working set is not a surprise. Each step is rounded **down** to a weight the
/// equipment can make, so no warm-up ever lands heavier than its percentage.
struct WarmUpRamp: Equatable, Sendable {
    struct Step: Equatable, Sendable, Identifiable {
        let weight: Double
        let reps: Int

        var id: String { "\(weight)x\(reps)" }
    }

    let steps: [Step]
    let unit: WeightUnit

    var isEmpty: Bool { steps.isEmpty }

    /// How a movement is loaded, which decides where the ramp starts and how its weights round.
    enum Loading: Equatable, Sendable {
        /// Plates on a bar: the empty bar is the first step, and weights round to the plate pairs.
        case barbell(PlateSet)
        /// Plate on top of bodyweight — a weighted dip or pull-up. The first step is bodyweight
        /// alone, logged as nought added.
        case addedToBodyweight
        /// A stack, dumbbells, a machine: percentages of the working weight, rounded to the unit's
        /// usual step.
        case other
    }

    /// The ramp to `working`, or an empty one where the working weight is too light to need one.
    static func to(_ working: Double, unit: WeightUnit, loading: Loading) -> WarmUpRamp {
        guard working > 0 else { return WarmUpRamp(steps: [], unit: unit) }

        let candidates: [Step]
        switch loading {
        case .barbell(let plates):
            guard working > plates.bar else { return WarmUpRamp(steps: [], unit: unit) }
            candidates = [Step(weight: plates.bar, reps: 10)]
                + [(0.5, 5), (0.7, 3), (0.85, 1)].map { fraction, reps in
                    Step(weight: loadable(working * fraction, floor: plates.bar, step: plates.increment), reps: reps)
                }
        case .addedToBodyweight:
            candidates = [Step(weight: 0, reps: 8)]
                + [(0.6, 3)].map { fraction, reps in
                    Step(weight: rounded(working * fraction, step: step(for: unit)), reps: reps)
                }
        case .other:
            candidates = [(0.5, 8), (0.75, 4)].map { fraction, reps in
                Step(weight: rounded(working * fraction, step: step(for: unit)), reps: reps)
            }
        }

        // Strictly rising and strictly under the work: a step that rounds onto the one before it, or
        // up to the working weight, is not a warm-up of its own.
        let steps = candidates.reduce(into: [Step]()) { kept, step in
            let floor = kept.last?.weight ?? -1
            guard step.weight < working, step.weight > floor else { return }
            kept.append(step)
        }
        return WarmUpRamp(steps: steps, unit: unit)
    }

    /// The usual smallest change of load in a unit, away from a bar.
    private static func step(for unit: WeightUnit) -> Double {
        switch unit {
        case .kilograms: 2.5
        case .pounds, .unrecognised: 5
        }
    }

    private static func rounded(_ weight: Double, step: Double) -> Double {
        (weight / step).rounded(.down) * step
    }

    /// The heaviest weight the plates make at or under `weight`, and never under the bar.
    private static func loadable(_ weight: Double, floor bar: Double, step: Double) -> Double {
        max(bar, bar + ((weight - bar) / step).rounded(.down) * step)
    }
}
