import Foundation
import WidgetKit

/// Hands the widgets what Today has just read, and asks WidgetKit to redraw only when that changed.
///
/// Today's store calls in with every current reading of today; most of them say what the last one
/// said, and a widget reload spends a budget the system rations, so an unchanged glance is neither
/// written nor reloaded. The comparison ignores `asOf`, which changes on every read and is not on
/// any widget.
@MainActor
final class TodayGlancePublisher {
    private let file: TodayGlanceFile
    private var published: TodayGlance?
    /// The reading the glance was last made from, kept so it can be named again.
    private var source: (snapshot: TodaySnapshot, asOf: Date)?

    init(file: TodayGlanceFile = TodayGlanceFile()) {
        self.file = file
        published = file.read()
    }

    func publish(_ snapshot: TodaySnapshot, catalogue: MovementCatalogue, asOf: Date) {
        source = (snapshot, asOf)
        let glance = Self.glance(of: snapshot, catalogue: catalogue, asOf: asOf)
        guard !Self.drawsTheSame(glance, as: published) else { return }
        do {
            try file.write(glance)
            published = glance
            WidgetCenter.shared.reloadAllTimelines()
        } catch {
            // A glance that could not be written leaves the widgets on the last one, which is
            // dated by its day; nothing on screen in the app depends on it.
        }
    }

    /// Names the last reading's sessions again with a catalogue that has since loaded — the first
    /// launch reads today before it has the movements' names.
    func rename(with catalogue: MovementCatalogue) {
        guard let source else { return }
        publish(source.snapshot, catalogue: catalogue, asOf: source.asOf)
    }

    /// Clears the widgets. What signing out does: the next athlete must not see this one's
    /// readiness on the Lock Screen.
    func clear() {
        file.remove()
        published = nil
        source = nil
        WidgetCenter.shared.reloadAllTimelines()
    }

    /// Today's reading as the widgets show it.
    ///
    /// The last session is the newest completed one on the day or, when none is, the last before
    /// it, which is the one Today calls "Last session".
    static func glance(of snapshot: TodaySnapshot, catalogue: MovementCatalogue, asOf: Date) -> TodayGlance {
        let today = snapshot.day
        let readiness = snapshot.recovery.flatMap { recovery in
            recovery.readiness.map { reading in
                TodayGlance.Readiness(
                    score: reading.score.rounded,
                    band: reading.band.label,
                    asksForAttention: reading.band == .payAttention,
                    comparison: recovery.readinessFigure?.deviation.map {
                        "\(Formatting.deviation($0, of: .readiness)) vs baseline"
                    },
                    recent: recovery.readinessFigure?.recent.map(\.value) ?? [],
                    day: reading.day.middayDate())
            }
        }
        let completedToday = snapshot.sessionsOnDay
            .compactMap { session -> TrainingSession.Completed? in
                if case .completed(let completed) = session { return completed }
                return nil
            }
            .last
        let last = (completedToday ?? snapshot.lastCompleted).map(TrainingSession.completed)

        return TodayGlance(
            readiness: readiness,
            lastSession: last.map { line(for: $0, catalogue: catalogue, today: today) },
            nextSession: snapshot.nextPlanned.map {
                line(for: .planned($0), catalogue: catalogue, today: today)
            },
            asOf: asOf)
    }

    private static func line(
        for session: TrainingSession, catalogue: MovementCatalogue, today: CalendarDay
    ) -> TodayGlance.Session {
        let row = SessionLog.row(for: session, catalogue: catalogue, today: today)
        return TodayGlance.Session(
            day: row.day.middayDate(), headline: row.headline, facts: Formatting.sessionFacts(row.facts))
    }

    private static func drawsTheSame(_ glance: TodayGlance, as other: TodayGlance?) -> Bool {
        guard let other else { return false }
        return glance.readiness == other.readiness
            && glance.lastSession == other.lastSession
            && glance.nextSession == other.nextSession
    }
}
