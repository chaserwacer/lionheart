import SwiftUI
import WidgetKit

/// The widget extension's entry point.
///
/// One `ActivityConfiguration` and no home-screen widget: this target exists so the workout's Live
/// Activity has a process to be drawn in, which is the only way WidgetKit renders one.
@main
struct LionheartWorkoutActivityBundle: WidgetBundle {
    var body: some Widget {
        WorkoutActivity()
    }
}
