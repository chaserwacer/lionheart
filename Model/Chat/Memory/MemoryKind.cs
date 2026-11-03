namespace lionheart.Model.Chat.Memory;

/// <summary>
/// What sort of thing the athlete told the chat. The kind decides whether a memory is given to
/// every session or only recalled when it bears on one.
/// </summary>
/// <remarks>Integer-valued on the wire, like every other enum this API sends.</remarks>
public enum MemoryKind
{
    /// <summary>Something he is working toward.</summary>
    Goal,

    /// <summary>An injury or niggle that is current. One that has healed is a <see cref="Fact"/> recording when.</summary>
    Injury,

    /// <summary>A standing way he wants to train or be spoken to.</summary>
    Preference,

    /// <summary>Something he said he would do, with or without a date.</summary>
    Commitment,

    /// <summary>A choice he made about his training, and its reason where he gave one.</summary>
    Decision,

    /// <summary>Anything else about him worth keeping: circumstances, history, equipment, schedule.</summary>
    Fact
}

public static class MemoryKinds
{
    extension(MemoryKind kind)
    {
        /// <summary>
        /// Whether every session starts with memories of this kind, whatever it is about: what he
        /// is aiming at, what hurts, and how he wants things done. The rest are recalled when they
        /// bear on the session.
        /// </summary>
        public bool IsCore => kind is MemoryKind.Goal or MemoryKind.Injury or MemoryKind.Preference;

        /// <summary>The kind as a model reads and writes it: <c>goal</c>, <c>injury</c>.</summary>
        public string Word => kind.ToString().ToLowerInvariant();
    }
}
