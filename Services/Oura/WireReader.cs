using System.Globalization;
using lionheart.Functional;
using lionheart.Model.Analysis;
using lionheart.Model.Oura.Readings;
using lionheart.Model.Oura.Wire;

namespace lionheart.Services.Oura;

/// <summary>One field of an Oura document that was missing or unreadable, and what was wrong with it.</summary>
public sealed record OuraDocumentDefect(string Field, string Detail);

/// <summary>
/// Everything wrong with one Oura document. Plural because a document is reported on once: a
/// payload with four unreadable fields should say so, not send the reader back four times.
/// </summary>
public sealed record OuraDocumentDefects(IReadOnlyList<OuraDocumentDefect> Defects)
{
    public string Description =>
        string.Join("; ", Defects.Select(defect => $"{defect.Field}: {defect.Detail}"));
}

/// <summary>
/// Pulls typed values out of an Oura wire record, collecting every field it could not read instead
/// of stopping at the first.
/// </summary>
/// <remarks>
/// Accumulating rather than fail-fast is the right shape at a wire boundary, and C# has no way to
/// spell a twenty-input <c>ZipAll</c>, so the accumulation lives in this small reader instead. A
/// pull that fails records a defect and answers with a harmless, non-null default, which lets
/// <see cref="Build{T}"/> run the caller's constructor without tripping a domain guard and then
/// discard what it built — so each mapper stays one expression with every required field named
/// where it is read.
/// <para>
/// It is deliberately scoped to this module rather than promoted into the monad layer: one wire
/// layer needs it today. A second one is the moment to move it.
/// </para>
/// </remarks>
internal sealed class WireReader
{
    private static readonly OuraSampleSeries EmptySeries = new(0, [], default);

    private readonly List<OuraDocumentDefect> _defects = [];

    /// <summary>
    /// The value the caller's constructor produced, or every defect found while reading its fields.
    /// </summary>
    /// <remarks>
    /// The constructor runs before the defects are counted, because the field pulls happen inside
    /// it. On a defective payload it therefore runs over this reader's stand-in values and its
    /// answer is thrown away. Those stand-ins are chosen to be harmless, but a reading record's
    /// own guard is entitled to reject one — <see cref="Analysis.Interval"/> already does, which is
    /// why <see cref="Span"/> exists — and a guard that throws here would end an import rather than
    /// report a record. So a throwing constructor is caught and becomes the defect it is: the
    /// payload could not be built. Only the exception types a domain guard raises are caught;
    /// anything else is a bug and propagates.
    /// </remarks>
    public Result<T, OuraDocumentDefects> Build<T>(Func<T> build)
    {
        T built;
        try
        {
            built = build();
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
        {
            _defects.Add(new OuraDocumentDefect(
                "<document>", $"could not be constructed ({exception.Message})"));
            return Result<T, OuraDocumentDefects>.Failure(new OuraDocumentDefects(_defects));
        }

        return _defects.Count is 0
            ? Result<T, OuraDocumentDefects>.Success(built)
            : Result<T, OuraDocumentDefects>.Failure(new OuraDocumentDefects(_defects));
    }

    /// <summary>Oura's document id, which is a UUID and becomes the row's primary key.</summary>
    public Guid Id(string? value) =>
        Guid.TryParse(value, out var id) && id != Guid.Empty
            ? id
            : Defect("id", value is null ? "missing" : $"not a UUID: '{value}'", Guid.Empty);

    public DateOnly Day(string? value) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day)
            ? day
            : Defect("day", value is null ? "missing" : $"not a yyyy-MM-dd date: '{value}'", default(DateOnly));

    public DateTimeOffset Instant(string? value, string field) =>
        DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant)
            ? instant
            : Defect(field, value is null ? "missing" : $"not an ISO-8601 instant: '{value}'", default(DateTimeOffset));

    /// <summary>
    /// The span an event occupied, from its two endpoints.
    /// </summary>
    /// <remarks>
    /// <see cref="Interval"/> refuses an end before its start by throwing, which is right for a
    /// domain caller and wrong here: one reversed pair in an archive file must be a rejected
    /// record, not a dead import. Both endpoints are read before the comparison so a payload with
    /// two unreadable instants reports both.
    /// </remarks>
    public Period Span(string? start, string? end, string startField, string endField)
    {
        var from = Instant(start, startField);
        var to = Instant(end, endField);

        return to >= from
            ? new Interval(from, to)
            : Defect<Period>(endField, $"ends at {to:O}, before its start at {from:O}", new Instant(from));
    }

    public int Int(int? value, string field) => value ?? Defect(field, "missing", 0);

    public double Double(double? value, string field) => value ?? Defect(field, "missing", 0d);

    public string Text(string? value, string field) =>
        string.IsNullOrEmpty(value) ? Defect(field, value is null ? "missing" : "empty", string.Empty) : value;

    /// <summary>A nested object Oura always sends, mapped by the caller once it is known present.</summary>
    public T Object<TWire, T>(TWire? wire, string field, Func<TWire, T> map, T whenMissing)
        where TWire : class =>
        wire is null ? Defect(field, "missing", whenMissing) : map(wire);

    /// <summary>
    /// A sample run. Oura sends the envelope on every record that can carry one, so an absent
    /// series is a malformed payload rather than a night without samples — a night the ring read
    /// nothing on arrives as a full-length run of nulls.
    /// </summary>
    public OuraSampleSeries Series(WireSampleSeries? value, string field) =>
        Object(value, field, series => new OuraSampleSeries(
            Double(series.Interval, $"{field}.interval"),
            series.Items is null
                ? Defect<IReadOnlyList<double?>>($"{field}.items", "missing", [])
                : series.Items,
            Instant(series.Timestamp, $"{field}.timestamp")),
            EmptySeries);

    public IReadOnlyList<string> TextList(List<string?>? value, string field)
    {
        if (value is null) return Defect<IReadOnlyList<string>>(field, "missing", []);
        if (value.Any(string.IsNullOrEmpty)) return Defect<IReadOnlyList<string>>(field, "holds an empty element", []);

        return [.. value!];
    }

    private T Defect<T>(string field, string detail, T fallback)
    {
        _defects.Add(new OuraDocumentDefect(field, detail));
        return fallback;
    }
}
