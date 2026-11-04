import Foundation

/// The unit a run's page reads distance and pace in: kilometres or miles.
///
/// The server sends paces per kilometre and splits in both units; reading a page in miles converts
/// a pace by the length of a mile, which is a change of unit, not a second derivation of the pace.
enum RunUnit: String, CaseIterable, Sendable {
    case kilometres
    case miles

    var meters: Double {
        switch self {
        case .kilometres: 1000
        case .miles: 1609.344
        }
    }

    var abbreviation: String {
        switch self {
        case .kilometres: "km"
        case .miles: "mi"
        }
    }

    /// A distance in metres, in this unit, to two places: "10.21 km".
    func distance(_ meters: Double) -> String {
        String(format: "%.2f %@", meters / self.meters, abbreviation)
    }

    /// A pace given per kilometre, as time per this unit: "5:07 /km", "8:15 /mi".
    func pace(perKilometre seconds: TimeInterval) -> String {
        "\(MeasurementText.clock(seconds: (seconds * meters / 1000).rounded())) /\(abbreviation)"
    }

    /// A split's own pace, which the server gives per the split's unit: "5:07".
    static func splitPace(_ split: RunSplit) -> String {
        MeasurementText.clock(seconds: split.pace.rounded())
    }

    /// The run's splits in this unit.
    func splits(of run: RecordedRun) -> [RunSplit] {
        switch self {
        case .kilometres: run.kilometres
        case .miles: run.miles
        }
    }
}
