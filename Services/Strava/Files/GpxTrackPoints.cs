using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using lionheart.Functional;

namespace lionheart.Services.Strava.Files;

/// <summary>Reads the track points out of a GPX file as Strava writes one.</summary>
/// <remarks>
/// Strava's GPX holds one track of segments of <c>trkpt</c>s, each with its position, an elevation
/// and a time, and — where the activity had a sensor — a Garmin track-point extension carrying the
/// heart rate and cadence. Elements are found by local name, so the namespace a writer declared
/// for its extension does not matter, and a power reading, which some writers put in the
/// extension and some in a <c>power</c> element of their own, is read either way. A point
/// without a time cannot be placed on the workout's clock, so a file holding one is refused.
/// </remarks>
internal static class GpxTrackPoints
{
    public static Result<IReadOnlyList<TrackSample>, string> Read(Stream gpx)
    {
        try
        {
            using var reader = XmlReader.Create(gpx, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit });
            var points = XDocument.Load(reader).Descendants().Where(element => element.Name.LocalName == "trkpt").ToList();

            return points.Traverse(Sample);
        }
        catch (XmlException e)
        {
            return Result<IReadOnlyList<TrackSample>, string>.Failure($"The GPX file is not well-formed XML: {e.Message}");
        }
    }

    private static Result<TrackSample, string> Sample(XElement point) =>
        Child(point, "time") is { } time && DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
            ? Number(point.Attribute("lat")?.Value) is { } latitude && Number(point.Attribute("lon")?.Value) is { } longitude
                ? Result<TrackSample, string>.Success(new TrackSample(
                    at,
                    latitude,
                    longitude,
                    Number(Child(point, "ele")),
                    Number(Extension(point, "hr")) is { } bpm ? (int)Math.Round(bpm) : null,
                    Number(Extension(point, "cad")),
                    Number(Extension(point, "power")),
                    Speed: null))
                : Result<TrackSample, string>.Failure($"The track point at {at:O} has no position.")
            : Result<TrackSample, string>.Failure("A track point has no time.");

    private static string? Child(XElement point, string name) =>
        point.Elements().FirstOrDefault(element => element.Name.LocalName == name)?.Value;

    private static string? Extension(XElement point, string name) =>
        point.Elements().Where(element => element.Name.LocalName == "extensions")
            .SelectMany(extensions => extensions.Descendants())
            .FirstOrDefault(element => element.Name.LocalName == name)?.Value;

    private static double? Number(string? text) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : null;
}
