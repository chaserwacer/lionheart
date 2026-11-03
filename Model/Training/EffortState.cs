using System.Text.Json.Serialization;

namespace lionheart.Model.Training;

/// <summary>Perceived-effort fill-state: reported or not — never a nullable ratings object.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(EffortNotReported), "none")]
[JsonDerivedType(typeof(EffortReported), "reported")]
public abstract record EffortState;

public sealed record EffortNotReported : EffortState;

public sealed record EffortReported(PerceivedEffortRatings Ratings) : EffortState;
