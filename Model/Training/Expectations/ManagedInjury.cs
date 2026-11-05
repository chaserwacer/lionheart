using lionheart.Model.Training.Patterns;

namespace lionheart.Model.Training.Expectations;

/// <summary>
/// An injury open on the day as the week manages around it: the families whose targets pause, the
/// movements to leave out, and the trend in the weeks that led up to it.
/// </summary>
/// <remarks>
/// The families are the ones the athlete named on the injury, joined by the families of every
/// movement its check-ins name, so an injury logged with no families still acts once a check-in
/// says what hurt. While it is open each family's target pauses — asked nothing, so the adjusted
/// score holds nothing against it — and the advice is to hold that family's volume rather than
/// build it; once he resolves it the target grows back from the four weeks before at the usual
/// ten per cent, which after weeks of holding is the gentle ramp back.
/// </remarks>
/// <param name="Since">The day the injury is dated from.</param>
/// <param name="Movements">The movements its check-ins name, to be left out of the next session.</param>
/// <param name="LastPain">The newest check-in's pain, 0–10, when there is one.</param>
/// <param name="Preceding">Each affected family's hard sets in the four weeks before the injury against the eight before those.</param>
public sealed record ManagedInjury(
    Guid InjuryId,
    string Name,
    DateOnly Since,
    IReadOnlyList<PatternFamily> Families,
    IReadOnlyList<Guid> Movements,
    int? LastPain,
    IReadOnlyList<FamilyTrend> Preceding);

/// <summary>A family's weekly hard sets in the four weeks before a day against the eight weeks before those.</summary>
/// <param name="Recent">The mean weekly hard sets of the four weeks before.</param>
/// <param name="Earlier">The mean weekly hard sets of the eight weeks before those.</param>
public sealed record FamilyTrend(PatternFamily Family, double Recent, double Earlier)
{
    /// <summary>The rise of the recent weeks over the earlier ones as a share; zero with nothing earlier to compare.</summary>
    public double Change => Earlier > 0 ? Recent / Earlier - 1 : 0;
}
