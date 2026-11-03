using lionheart.Services.Llm;

namespace lionheart.Services.Profile
{
    /// <summary>
    /// The <b>cheap</b> model, used exclusively for off-the-hot-path narrative summarization when
    /// building Athlete Context Cards.
    /// </summary>
    /// <remarks>
    /// Wrapping (rather than registering a second bare <see cref="IChatModel"/>) keeps DI
    /// unambiguous: the flagship chat model and the cheap narrative model are different singletons
    /// of the same type. Card numbers are always deterministic; this model only writes prose over
    /// already-computed facts, so its failure must never break card generation.
    /// </remarks>
    public sealed class NarrativeChatClient(IChatModel model)
    {
        public IChatModel Model { get; } = model;
    }
}
