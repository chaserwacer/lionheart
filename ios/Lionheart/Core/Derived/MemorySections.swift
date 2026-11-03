import Foundation

/// The memories of one kind, as the memory screen groups them.
struct MemorySection: Equatable, Sendable, Identifiable {
    let kind: MemoryKind
    let memories: [ChatMemory]

    var id: Int { kind.wire }

    var title: String { Self.title(of: kind) }

    static func title(of kind: MemoryKind) -> String {
        switch kind {
        case .goal: "Goals"
        case .injury: "Injuries"
        case .preference: "Preferences"
        case .commitment: "Commitments"
        case .decision: "Decisions"
        case .fact: "Facts"
        case .unrecognised: "Other"
        }
    }

    /// The singular, for a picker or a detail row.
    static func name(of kind: MemoryKind) -> String {
        switch kind {
        case .goal: "Goal"
        case .injury: "Injury"
        case .preference: "Preference"
        case .commitment: "Commitment"
        case .decision: "Decision"
        case .fact: "Fact"
        case .unrecognised: "Other"
        }
    }

    /// One section per kind that has memories, in the order the kinds are listed — standing kinds
    /// first, since those reach every session — newest first within each. Kinds this build does not
    /// know come last, together.
    static func of(_ memories: [ChatMemory]) -> [MemorySection] {
        let known = MemoryKind.known.compactMap { kind -> MemorySection? in
            let ofKind = memories.filter { $0.kind == kind }
            return ofKind.isEmpty ? nil : MemorySection(kind: kind, memories: newestFirst(ofKind))
        }
        let other = memories.filter { !MemoryKind.known.contains($0.kind) }
        return known + (other.isEmpty ? [] : [MemorySection(kind: .unrecognised(-1), memories: newestFirst(other))])
    }

    private static func newestFirst(_ memories: [ChatMemory]) -> [ChatMemory] {
        memories.sorted { ($0.recordedAt ?? .distantPast) > ($1.recordedAt ?? .distantPast) }
    }
}
