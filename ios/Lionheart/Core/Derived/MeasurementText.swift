import Foundation

/// How a measured number reads.
///
/// One rule, in `Core/`, because both the derivations and the screens need it and three copies of
/// it is three chances for `92.5 kg` and `100.0 kg` to end up side by side. It is not in
/// `Support/Formatting` because it is not a choice of words — it is what the number *is*, and the
/// set readings in `Core/` are tested against it.
enum MeasurementText {
    /// A measurement without a trailing `.0`, so "92.5 kg" and "100 kg" read the same way, and to
    /// one decimal otherwise — the precision every measurement in this domain is stated to.
    static func plain(_ value: Double) -> String {
        guard value.isFinite else { return "—" }
        return value == value.rounded() ? String(Int(value)) : String(format: "%.1f", value)
    }

    /// A span of seconds as a clock — `24:10`, or `1:18:00` once there are hours.
    static func clock(seconds: TimeInterval) -> String {
        let whole = Int(max(0, seconds))
        return whole >= 3600
            ? String(format: "%d:%02d:%02d", whole / 3600, (whole % 3600) / 60, whole % 60)
            : String(format: "%d:%02d", whole / 60, whole % 60)
    }

    /// A span of seconds in words — "48 min", "7 h 20 min". Oura reports sleep in seconds and
    /// nobody reads sleep in seconds.
    static func spelledDuration(seconds: TimeInterval) -> String {
        let minutes = Int((max(0, seconds) / 60).rounded())
        guard minutes >= 60 else { return "\(minutes) min" }
        return minutes % 60 == 0 ? "\(minutes / 60) h" : "\(minutes / 60) h \(minutes % 60) min"
    }
}
