namespace lionheart.Functional;

/// <summary>One field a request got wrong, and what is wrong with it.</summary>
public sealed record InvalidField(string Field, string Message);

/// <summary>
/// Why an operation refused. A closed set of cases rather than a string or a status enum, so a
/// caller can branch on the kind of refusal and the transport edge can map it exactly once.
/// Nothing here knows about HTTP.
/// </summary>
public abstract record Problem
{
    private protected Problem() { }

    /// <summary>
    /// One line a human — or a model reading back a failed tool call — can act on. Every case
    /// answers it, so no caller has to reach into a particular case to find the message.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>The thing asked for does not exist, or does not belong to this user.</summary>
    public sealed record NotFound(string What) : Problem
    {
        public override string Description => What;
    }

    /// <summary>No identity, or an identity that cannot be established.</summary>
    public sealed record Unauthorized(string Reason) : Problem
    {
        public override string Description => Reason;
    }

    /// <summary>A known identity that is not allowed to do this.</summary>
    public sealed record Forbidden(string Reason) : Problem
    {
        public override string Description => Reason;
    }

    /// <summary>The request itself is malformed. Carries every offending field, not just the first.</summary>
    public sealed record Invalid(IReadOnlyList<InvalidField> Fields) : Problem
    {
        public Invalid(string field, string message) : this([new InvalidField(field, message)]) { }

        public override string Description => string.Join("; ", Fields.Select(f => f.Message));
    }

    /// <summary>The request is well formed but collides with state that already exists.</summary>
    public sealed record Conflict(string What) : Problem
    {
        public override string Description => What;
    }

    /// <summary>
    /// A dependency outside this application — the LM Studio chat and embedding endpoints, the
    /// Oura API — failed or answered unusably. Distinct from <see cref="Unexpected"/> because it
    /// is not this codebase's defect and is often worth retrying.
    /// </summary>
    public sealed record Upstream(string Detail) : Problem
    {
        public override string Description => Detail;
    }

    /// <summary>A refusal with no better case. Reserve it; a recurring one is a case waiting to be named.</summary>
    public sealed record Unexpected(string Detail) : Problem
    {
        public override string Description => Detail;
    }
}
