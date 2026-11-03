import Foundation
import UserNotifications

/// The real notification-centre side of the rest clock.
///
/// This is what actually buzzes. The store's rest clock is a `Task`, which stops running the moment
/// the app is suspended, so a phone in a pocket between sets would otherwise never announce that
/// the rest was over. Handing the instant to the system instead means it fires whether the app is
/// suspended, backgrounded or force-quit — and it needs no entitlement, no APNs and no paid
/// membership, which is what makes it the alert this app can actually have.
///
/// Everything here is best-effort and silent, like the Live Activity and for the same reason: the
/// alert is a convenience on top of a rest clock that is already correct on screen. A refused
/// permission, a throwing `add`, a system that is not in the mood — none of it is a failure the
/// athlete hears about, and none of it can stop a set being logged.
@MainActor
final class RestNotification: RestAlarmScheduling {
    /// One pending alert at a time, so the identifier is a constant: scheduling a new rest
    /// replaces the previous request rather than stacking a second buzz behind it.
    private static let identifier = "lh.rest.ended"

    /// The alarm last handed to the system.
    ///
    /// The same dedupe the Live Activity does, and it matters more here: the store mirrors on every
    /// mutation, and re-adding a request recomputes its trigger from the current instant, so a
    /// mutation that has nothing to do with the rest would otherwise nudge the fire time on every
    /// logged set.
    private var scheduled: PendingRestAlarm?

    /// Whether the athlete has been asked yet. Asking is a prompt, so it happens once, at the first
    /// rest that wants it.
    private var hasAsked = false

    private let center: UNUserNotificationCenter

    init(center: UNUserNotificationCenter = .current()) {
        self.center = center
    }

    func schedule(_ alarm: PendingRestAlarm?) async {
        guard let alarm else {
            withdraw()
            return
        }
        guard scheduled != alarm else { return }

        // Whatever is pending is now wrong, so it comes down **before** anything else is tried.
        // Doing it the other way round — only replacing on success — leaves the previous rest's
        // alert armed on every path that does not reach `add`: a refused permission, a revoked
        // one, a rest shortened to nothing. The phone then buzzes at the old rest's end instead of
        // the new one's, which is the failure this whole design exists to prevent, wearing a
        // different costume.
        withdraw()

        let seconds = alarm.secondsRemaining(at: Date())
        // A trigger refuses a non-positive interval, and an alarm that late has nothing to announce
        // anyway — the athlete is already past it.
        guard seconds > 0 else { return }

        // Asked here rather than at launch: the first armed rest is the moment the permission is
        // obviously for, and an app that prompts before the athlete has used it earns a refusal.
        guard await isPermitted() else { return }

        let content = UNMutableNotificationContent()
        content.title = alarm.title
        content.body = alarm.body
        content.sound = .default
        // The rest is the only thing this app interrupts for, and an alert that arrives late is an
        // alert about nothing. This is **inert on the current Apple tier**: the level is honoured
        // only with the time-sensitive entitlement, which needs a paid membership, and is silently
        // treated as `.active` without one. Left in because it costs a line and starts working the
        // day that changes, rather than being rediscovered then.
        content.interruptionLevel = .timeSensitive

        let request = UNNotificationRequest(
            identifier: Self.identifier,
            content: content,
            trigger: UNTimeIntervalNotificationTrigger(timeInterval: seconds, repeats: false))

        do {
            try await center.add(request)
            scheduled = alarm
        } catch {
            scheduled = nil
        }
    }

    func forgetDelivered() async {
        center.removeDeliveredNotifications(withIdentifiers: [Self.identifier])
    }

    private func withdraw() {
        center.removePendingNotificationRequests(withIdentifiers: [Self.identifier])
        scheduled = nil
    }

    /// Whether the system will deliver an alert, asking once if it has not been asked.
    ///
    /// A refusal is remembered only as "do not ask again this launch"; it changes nothing else.
    /// The rest clock still runs, the bar still counts it down and the Lock Screen still renders
    /// its countdown — a declined permission costs the buzz and nothing more.
    private func isPermitted() async -> Bool {
        if await center.notificationSettings().authorizationStatus == .authorized { return true }
        guard !hasAsked else { return false }
        hasAsked = true
        return (try? await center.requestAuthorization(options: [.alert, .sound])) ?? false
    }
}
