using System.Diagnostics;

namespace lionheart.Services.Training.Expectations;

/// <summary>Whether an injury is still open, and if not, whether the day it closed is known.</summary>
public abstract record InjuryStanding
{
    private protected InjuryStanding() { }

    public sealed record Active : InjuryStanding;

    public sealed record Resolved(DateOnly On) : InjuryStanding;

    /// <summary>Resolved before the day was kept: nothing says which weeks it covered.</summary>
    public sealed record ResolvedUndated : InjuryStanding;

    public static InjuryStanding Of(bool isActive, DateOnly? resolvedOn) =>
        (isActive, resolvedOn) switch
        {
            (true, _) => new Active(),
            (false, DateOnly on) => new Resolved(on),
            (false, null) => new ResolvedUndated(),
        };

    /// <summary>Still open on the day: active, or resolved on a later day. An undated resolution is open on none.</summary>
    public bool OpenOn(DateOnly day) => this switch
    {
        Active => true,
        Resolved resolved => day < resolved.On,
        ResolvedUndated => false,
        _ => throw new UnreachableException(),
    };
}
