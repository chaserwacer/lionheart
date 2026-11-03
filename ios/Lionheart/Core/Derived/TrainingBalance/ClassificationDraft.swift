import Foundation

/// A movement's classification as the athlete edits it, before the server has it.
///
/// A muscle is primary, secondary or neither, never two at once, so marking it one takes it out of
/// the other. Primaries keep the order they were marked in, because the first names the family of
/// an isolation movement — the server's rule.
struct ClassificationDraft: Equatable, Sendable {
    var pattern: MovementPattern
    private(set) var primary: [MuscleGroup]
    private(set) var secondary: [MuscleGroup]

    enum Role: Equatable, Sendable { case primary, secondary }

    init(_ classification: MovementClassification?) {
        pattern = classification?.pattern ?? .isolation
        primary = classification?.primary ?? []
        secondary = classification?.secondary ?? []
    }

    func role(of muscle: MuscleGroup) -> Role? {
        if primary.contains(muscle) { return .primary }
        if secondary.contains(muscle) { return .secondary }
        return nil
    }

    /// Marks the muscle in the role, or clears it when it already holds that role.
    mutating func toggle(_ muscle: MuscleGroup, as role: Role) {
        let held = self.role(of: muscle)
        primary.removeAll { $0 == muscle }
        secondary.removeAll { $0 == muscle }
        guard held != role else { return }
        switch role {
        case .primary: primary.append(muscle)
        case .secondary: secondary.append(muscle)
        }
    }

    /// The classification to send, or `nil` while it names no primary muscle or an unknown pattern —
    /// the two things the server would refuse.
    var classification: MovementClassification? {
        guard !primary.isEmpty else { return nil }
        if case .unrecognised = pattern { return nil }
        return MovementClassification(pattern: pattern, primary: primary, secondary: secondary)
    }
}
