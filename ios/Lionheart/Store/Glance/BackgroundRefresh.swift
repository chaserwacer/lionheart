import BackgroundTasks
import Foundation

/// The one background task Lionheart asks for: a morning read that brings the widgets up to date.
///
/// Free developer accounts get Background Modes but not push, so a wake the system schedules is the
/// only way the Lock Screen's readiness can change before the app is opened. The task itself is the
/// scene's `.backgroundTask(.appRefresh(_:))` (`LionheartApp`); this type names it and asks for it.
enum BackgroundRefresh {
    /// Listed under `BGTaskSchedulerPermittedIdentifiers` in `ios/project.yml`.
    static let identifier = "com.chasebalmer.lionheart.morning-refresh"

    /// Asks for the next morning's wake, replacing any request already pending.
    ///
    /// Refusals are not errors worth surfacing: the simulator refuses every request, and a phone
    /// with Background App Refresh turned off refuses them by the athlete's choice.
    static func schedule(after now: Date = Date()) {
        let request = BGAppRefreshTaskRequest(identifier: identifier)
        request.earliestBeginDate = MorningRefresh.earliest(after: now)
        try? BGTaskScheduler.shared.submit(request)
    }
}
