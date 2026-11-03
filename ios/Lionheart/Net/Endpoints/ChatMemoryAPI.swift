import Foundation

/// What Lionheart remembers from the thread (`docs/api-surface.md` → Chat thread and memory).
///
/// A correction answers 409 when the memory it names has already been replaced — the list on screen
/// is out of date — and that arrives as `APIError.refused`.
struct ChatMemoryAPI: Sendable {
    let client: APIClient

    private static let root = "/api/chat/memories"

    func memories(remembering key: ReadKey) async throws -> Remembered<[ChatMemory]> {
        try await client.get(Self.root, as: LenientList<ChatMemory>.self, remembering: key)
            .map(\.elements)
    }

    func correct(_ memoryId: UUID, kind: MemoryKind, text: String) async throws -> ChatMemory {
        try await client.put(
            "\(Self.root)/\(memoryId.uuidString)", body: CorrectMemoryBody(kind: kind.wire, text: text))
    }

    func delete(_ memoryId: UUID) async throws {
        try await client.delete("\(Self.root)/\(memoryId.uuidString)")
    }
}

private struct CorrectMemoryBody: Encodable, Sendable {
    let kind: Int
    let text: String
}
