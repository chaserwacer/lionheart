using System.Buffers.Binary;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using lionheart.Tests.Support;

namespace lionheart.Tests.Strava;

/// <summary>
/// Strava archives for the import tests, invented in Strava's shape: its <c>activities.csv</c> header
/// as written (columns repeated and all), its quoting (the date always quoted, an empty description
/// as <c>""</c>), its blanks and zeros, and activity files as GPX and gzipped FIT.
/// </summary>
/// <remarks>
/// Every value is made up: the ids, the dates, the names, the figures, and the routes, which run due
/// north from the invented meadow <see cref="AHealthRun"/> uses. Nothing here comes from an archive.
/// </remarks>
internal static class StravaArchives
{
    public const string Header =
        "Activity ID,Activity Date,Activity Name,Activity Type,Activity Description,Elapsed Time,Distance,Max Heart Rate,"
        + "Relative Effort,Commute,Activity Private Note,Activity Gear,Filename,Athlete Weight,Bike Weight,Elapsed Time,Moving Time,"
        + "Distance,Max Speed,Average Speed,Elevation Gain,Elevation Loss,Elevation Low,Elevation High,Max Grade,Average Grade,"
        + "Average Positive Grade,Average Negative Grade,Max Cadence,Average Cadence,Max Heart Rate,Average Heart Rate,Max Watts,"
        + "Average Watts,Calories,Max Temperature,Average Temperature,Relative Effort,Total Work,Number of Runs,Uphill Time,"
        + "Downhill Time,Other Time,Perceived Exertion,Type,Start Time,Weighted Average Power,Power Count,Prefer Perceived Exertion,"
        + "Perceived Relative Effort,Commute,Total Weight Lifted,From Upload,Grade Adjusted Distance,Weather Observation Time,"
        + "Weather Condition,Weather Temperature,Apparent Temperature,Dewpoint,Humidity,Weather Pressure,Wind Speed,Wind Gust,"
        + "Wind Bearing,Precipitation Intensity,Sunrise Time,Sunset Time,Moon Phase,Bike,Gear,Precipitation Probability,"
        + "Precipitation Type,Cloud Cover,Weather Visibility,UV Index,Weather Ozone,Jump Count,Total Grit,Average Flow,Flagged,"
        + "Average Elapsed Speed,Dirt Distance,Newly Explored Distance,Newly Explored Dirt Distance,Activity Count,Total Steps,"
        + "Carbon Saved,Pool Length,Training Load,Intensity,Average Grade Adjusted Pace,Timer Time,Total Cycles,Recovery,With Pet,"
        + "Competition,Long Run,For a Cause,With Kid,Downhill Distance,Total Sets,Total Reps,Media,Tag";

    private static readonly int Width = Header.Split(',').Length;

    /// <summary>The zone sessions are placed in; tests pin it rather than read the machine's.</summary>
    public static TimeZoneInfo NewYork => TimeZoneInfo.FindSystemTimeZoneById("America/New_York");

    /// <summary>
    /// One summary row, at Strava's column positions. Elapsed time goes in both of Strava's columns
    /// for it, the distance in kilometres first and in metres second; the rest is left blank as
    /// Strava leaves a measure it has no value for.
    /// </summary>
    public static string Row(
        long id,
        DateTimeOffset start,
        string type = "Run",
        string name = "Morning Run",
        double elapsedSeconds = 1800,
        double movingSeconds = 1700,
        double meters = 5000,
        string climb = "",
        string description = "",
        string privateNote = "",
        string exertion = "",
        string averageHeartRate = "",
        string maxHeartRate = "",
        string file = "")
    {
        var fields = Enumerable.Repeat(string.Empty, Width).ToArray();
        fields[0] = id.ToString(CultureInfo.InvariantCulture);
        fields[1] = Quoted(start.UtcDateTime.ToString("MMM d, yyyy, h:mm:ss tt", CultureInfo.InvariantCulture));
        fields[2] = name;
        fields[3] = type;
        fields[4] = Quoted(description);
        fields[5] = elapsedSeconds.ToString("0", CultureInfo.InvariantCulture);
        fields[6] = (meters / 1000).ToString("0.00", CultureInfo.InvariantCulture);
        fields[9] = "false";
        fields[10] = privateNote.Length > 0 ? Quoted(privateNote) : string.Empty;
        fields[12] = file;
        fields[15] = elapsedSeconds.ToString("0.0", CultureInfo.InvariantCulture);
        fields[16] = movingSeconds.ToString("0.0", CultureInfo.InvariantCulture);
        fields[17] = meters.ToString("0.0", CultureInfo.InvariantCulture);
        fields[20] = climb;
        fields[30] = maxHeartRate;
        fields[31] = averageHeartRate;
        fields[43] = exertion;
        fields[50] = "false";
        fields[52] = "1.0";
        fields[102] = Quoted(string.Empty);
        return string.Join(",", fields);
    }

    private static string Quoted(string text) => $"\"{text.Replace("\"", "\"\"")}\"";

    /// <summary>
    /// A GPX file as Strava writes one: a fix every five seconds due north from the meadow at
    /// <paramref name="metersPerSecond"/>, each with an elevation and a time, and a heart rate in
    /// Garmin's track-point extension where <paramref name="bpm"/> is given.
    /// </summary>
    public static string Gpx(DateTimeOffset start, double meters, double metersPerSecond = 3, int? bpm = null)
    {
        var points = AHealthRun.North(start, meters, metersPerSecond).Select(fix =>
            $"""
               <trkpt lat="{fix.Latitude.ToString("0.0000000", CultureInfo.InvariantCulture)}" lon="{fix.Longitude.ToString("0.0000000", CultureInfo.InvariantCulture)}">
                <ele>{fix.Altitude!.Value.ToString("0.0", CultureInfo.InvariantCulture)}</ele>
                <time>{fix.At.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}</time>{(bpm is { } heart ? $"\n    <extensions>\n     <gpxtpx:TrackPointExtension>\n      <gpxtpx:hr>{heart}</gpxtpx:hr>\n     </gpxtpx:TrackPointExtension>\n    </extensions>" : string.Empty)}
               </trkpt>
            """);

        return $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx creator="StravaGPX" version="1.1" xmlns="http://www.topografix.com/GPX/1/1" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" xmlns:gpxtpx="http://www.garmin.com/xmlschemas/TrackPointExtension/v1">
             <metadata>
              <time>{start.UtcDateTime:yyyy-MM-ddTHH:mm:ssZ}</time>
             </metadata>
             <trk>
              <name>Morning Run</name>
              <type>running</type>
              <trkseg>
            {string.Join("\n", points)}
              </trkseg>
             </trk>
            </gpx>
            """;
    }

    /// <summary>An archive folder in the temp directory — <c>activities.csv</c> and <c>activities/</c> — deleted on dispose.</summary>
    public sealed class ArchiveFolder : IDisposable
    {
        public ArchiveFolder(params string[] rows)
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"strava-archive-{Guid.NewGuid():N}");
            Directory.CreateDirectory(System.IO.Path.Combine(Path, "activities"));
            File.WriteAllText(System.IO.Path.Combine(Path, "activities.csv"), string.Join("\n", [Header, .. rows]) + "\n", Encoding.UTF8);
        }

        public string Path { get; }

        /// <summary>Writes an activity file at the path the summary names, relative to the archive.</summary>
        public ArchiveFolder With(string relativePath, string text) => With(relativePath, Encoding.UTF8.GetBytes(text));

        public ArchiveFolder With(string relativePath, byte[] content)
        {
            var path = System.IO.Path.Combine(Path, relativePath);
            if (relativePath.EndsWith(".gz", StringComparison.Ordinal))
            {
                using var file = File.Create(path);
                using var gzip = new GZipStream(file, CompressionLevel.Fastest);
                gzip.Write(content);
            }
            else
            {
                File.WriteAllBytes(path, content);
            }

            return this;
        }

        public void Dispose() => Directory.Delete(Path, recursive: true);
    }
}

/// <summary>
/// FIT files written field by field, for the reader's tests: a header, definitions and data
/// messages laid out as the protocol says, with whatever the test needs in them.
/// </summary>
internal sealed class FitFile
{
    private readonly List<byte> _messages = [];

    /// <summary>Seconds since FIT's epoch, the last day of 1989.</summary>
    public static uint Timestamp(DateTimeOffset at) => (uint)(at - new DateTimeOffset(1989, 12, 31, 0, 0, 0, TimeSpan.Zero)).TotalSeconds;

    public static int Semicircles(double degrees) => (int)Math.Round(degrees * 2147483648.0 / 180.0);

    /// <param name="fields">Field number, size and base type, in the order data messages carry them.</param>
    /// <param name="developerFieldSizes">The sizes of developer fields after them, which a reader skips.</param>
    public FitFile Define(byte local, ushort global, (byte Number, byte Size, byte BaseType)[] fields, bool bigEndian = false, byte[]? developerFieldSizes = null)
    {
        _messages.Add((byte)(0x40 | (developerFieldSizes is null ? 0 : 0x20) | local));
        _messages.Add(0);
        _messages.Add((byte)(bigEndian ? 1 : 0));
        var globalBytes = new byte[2];
        if (bigEndian) BinaryPrimitives.WriteUInt16BigEndian(globalBytes, global);
        else BinaryPrimitives.WriteUInt16LittleEndian(globalBytes, global);
        _messages.AddRange(globalBytes);
        _messages.Add((byte)fields.Length);
        foreach (var (number, size, baseType) in fields) _messages.AddRange([number, size, baseType]);
        if (developerFieldSizes is not null)
        {
            _messages.Add((byte)developerFieldSizes.Length);
            for (var i = 0; i < developerFieldSizes.Length; i++) _messages.AddRange([(byte)i, developerFieldSizes[i], 0]);
        }

        return this;
    }

    /// <summary>A data message of a local type, its content already laid out.</summary>
    public FitFile Data(byte local, params byte[] content)
    {
        _messages.Add(local);
        _messages.AddRange(content);
        return this;
    }

    /// <summary>A data message whose header carries its time as five bits past the last full timestamp.</summary>
    public FitFile Compressed(byte local, byte secondsMod32, params byte[] content)
    {
        _messages.Add((byte)(0x80 | (local << 5) | (secondsMod32 & 0x1F)));
        _messages.AddRange(content);
        return this;
    }

    public byte[] Bytes()
    {
        var header = new byte[14];
        header[0] = 14;
        header[1] = 0x20;
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2), 2132);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), (uint)_messages.Count);
        ".FIT"u8.CopyTo(header.AsSpan(8));
        return [.. header, .. _messages, 0, 0];
    }

    public static byte[] U32(uint value, bool bigEndian = false)
    {
        var bytes = new byte[4];
        if (bigEndian) BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        else BinaryPrimitives.WriteUInt32LittleEndian(bytes, value);
        return bytes;
    }

    public static byte[] S32(int value, bool bigEndian = false) => U32(unchecked((uint)value), bigEndian);
}
