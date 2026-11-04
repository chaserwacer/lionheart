import SwiftUI
import WidgetKit

/// The Home and Lock Screen widgets' entry point.
///
/// A target of its own rather than a second bundle member beside the workout's Live Activity: these
/// read the App Group the app writes and nothing else, while the activity is driven by ActivityKit
/// from the workout store, and keeping them apart keeps each target's surface to one job.
@main
struct LionheartWidgetsBundle: WidgetBundle {
    var body: some Widget {
        ReadinessWidget()
        TodayWidget()
    }
}
