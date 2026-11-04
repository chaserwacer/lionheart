using System.Text.Json.Serialization;

namespace lionheart.Model.BodyMeasurements.Composition;

/// <summary>A scan as the athlete states it, to record or to correct one.</summary>
/// <remarks>
/// The sheet is wholly the athlete's to state — nothing on it is the server's — so the request
/// carries it in its domain shape, and <see cref="SheetRules"/> checks it before anything is
/// written.
/// <para>
/// <see cref="RecordsBodyweight"/> is the athlete's answer to what the scan's weight does to that
/// day's bodyweight. True makes it the day's measurement, replacing one already logged; false
/// leaves the day as it is, for a morning reading the athlete would rather keep than a 4 pm scan.
/// The review form asks when the day holds a different weight, or when it could not tell what the
/// day holds, so the two never disagree without the athlete having chosen it.
/// </para>
/// <para>
/// Every member is required on the wire: a scan sent without its day would otherwise be recorded,
/// with a bodyweight, on 0001-01-01.
/// </para>
/// </remarks>
public sealed record ScanRequest(
    [property: JsonRequired] DateOnly Day,
    [property: JsonRequired] TimeOnly Time,
    [property: JsonRequired] BodyCompositionSheet Sheet,
    [property: JsonRequired] bool RecordsBodyweight);

/// <summary>A recorded scan on the wire.</summary>
public sealed record ScanView(Guid Id, DateOnly Day, TimeOnly Time, BodyCompositionSheet Sheet)
{
    public static ScanView Of(BodyCompositionScan scan) => new(scan.ScanId, scan.Day, scan.Time, scan.Sheet);
}
