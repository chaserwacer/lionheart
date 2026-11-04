namespace lionheart.Model.BodyMeasurements.Composition;

/// <summary>One body-composition scan: when it was taken, and the sheet it printed.</summary>
/// <remarks>
/// The day and time are the sheet's own clock, as printed — local wall time at the analyser,
/// which is the time the athlete knows the scan by. An entity rather than a record because a
/// sheet entered with a slip is corrected in place and stays the same scan.
/// </remarks>
public sealed class BodyCompositionScan
{
    // EF binds this constructor, so the sheet is never a placeholder awaiting a property set.
    private BodyCompositionScan(Guid scanId, Guid userId, DateOnly day, TimeOnly time, BodyCompositionSheet sheet)
    {
        ScanId = scanId;
        UserId = userId;
        Day = day;
        Time = time;
        Sheet = sheet;
    }

    public Guid ScanId { get; private set; }
    public Guid UserId { get; private set; }
    public DateOnly Day { get; private set; }
    public TimeOnly Time { get; private set; }
    public BodyCompositionSheet Sheet { get; private set; }

    public static BodyCompositionScan Taken(
        Guid scanId, Guid userId, DateOnly day, TimeOnly time, BodyCompositionSheet sheet) =>
        new(scanId, userId, day, time, sheet);

    /// <summary>Replaces what was entered for this scan with a corrected reading of the sheet.</summary>
    public void Restate(DateOnly day, TimeOnly time, BodyCompositionSheet sheet)
    {
        Day = day;
        Time = time;
        Sheet = sheet;
    }
}
