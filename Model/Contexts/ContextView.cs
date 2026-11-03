using System.Diagnostics;
using System.Text.Json.Serialization;
using lionheart.Model.Analysis;
using lionheart.Model.Training;

namespace lionheart.Model.Contexts;

/// <summary>
/// A context on the wire: the domain's two kinds, with the decoration reduced to what a client may
/// read. See <see cref="Training.Sessions.TrainingSessionView"/> for why the envelope is restated
/// rather than the domain record serialized directly.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(ExertionContextView), "exertion")]
[JsonDerivedType(typeof(StateContextView), "state")]
public abstract record ContextView
{
    private protected ContextView(Context context)
    {
        ContextId = context.ContextId;
        Period = context.Period;
        Notes = context.Notes;
        Decoration = DecorationView.Of(context.Decoration);
    }

    public Guid ContextId { get; init; }
    public Period Period { get; init; }
    public string Notes { get; init; }
    public DecorationView Decoration { get; init; }

    public static ContextView Of(Context context) => context switch
    {
        ExertionContext exertion => new ExertionContextView(exertion),
        StateContext state => new StateContextView(state),
        _ => throw new UnreachableException()
    };
}

public sealed record ExertionContextView : ContextView
{
    public ExertionContextView(ExertionContext context) : base(context) => Effort = context.Effort;

    public EffortState Effort { get; init; }
}

public sealed record StateContextView : ContextView
{
    public StateContextView(StateContext context) : base(context) { }
}
