import Foundation

/// When the app asks iOS to wake it in the background: the next early morning.
///
/// What a background read is for is the Lock Screen's readiness. Oura files the night once the
/// athlete is awake and the ring has synced, so the useful moment is the morning, before he opens
/// the app — and asking for more than one wake a day only spends the system's patience with an app
/// that has nothing new to show. iOS treats the time as the earliest it may run, not a promise.
enum MorningRefresh {
    /// The hour and minute of the morning asked for, local time.
    static let hour = 5
    static let minute = 30

    /// The next morning at `hour:minute` strictly after `now`.
    static func earliest(after now: Date, calendar: Calendar = .current) -> Date {
        let morning = DateComponents(hour: hour, minute: minute)
        return calendar.nextDate(after: now, matching: morning, matchingPolicy: .nextTime) ?? now.addingTimeInterval(86_400)
    }
}
