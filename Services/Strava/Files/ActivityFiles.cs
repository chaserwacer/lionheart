using System.IO.Compression;
using lionheart.Functional;

namespace lionheart.Services.Strava.Files;

/// <summary>Opens the file the archive keeps for an activity and reads its track.</summary>
/// <remarks>
/// Strava keeps an activity's file in the format it was uploaded in: GPX for what its own app
/// recorded, FIT for a device's upload or a strength workout, either one gzipped. TCX is not read:
/// no archive this was written against holds one, and an activity whose file cannot be read is
/// still imported from its summary row, with the file named in the report.
/// </remarks>
internal static class ActivityFiles
{
    /// <param name="archive">The archive's folder, which the file's path in the summary is relative to.</param>
    /// <param name="file">The path the summary gives, or empty for an activity with no file.</param>
    public static Result<ActivityTrack, string> Read(string archive, string file)
    {
        if (file.Length is 0) return Result<ActivityTrack, string>.Success(ActivityTrack.None);

        var root = Path.GetFullPath(archive) + Path.DirectorySeparatorChar;
        var path = Path.GetFullPath(Path.Combine(root, file));
        if (!path.StartsWith(root, StringComparison.Ordinal))
            return Result<ActivityTrack, string>.Failure("The file's path leads outside the archive.");
        if (!File.Exists(path))
            return Result<ActivityTrack, string>.Failure("The archive has no such file.");

        var gzipped = path.EndsWith(".gz", StringComparison.OrdinalIgnoreCase);
        var format = Path.GetExtension(gzipped ? path[..^3] : path).ToLowerInvariant();

        return format switch
        {
            ".gpx" => Samples(path, gzipped, GpxTrackPoints.Read),
            ".fit" => Samples(path, gzipped, stream => FitRecords.Read(Bytes(stream))),
            _ => Result<ActivityTrack, string>.Failure($"Files of type '{format}' are not read."),
        };
    }

    private static Result<ActivityTrack, string> Samples(
        string path, bool gzipped, Func<Stream, Result<IReadOnlyList<TrackSample>, string>> read)
    {
        try
        {
            using var file = File.OpenRead(path);
            using var content = gzipped ? new GZipStream(file, CompressionMode.Decompress) : (Stream)file;
            return read(content).Map(ActivityTrack.Of);
        }
        catch (InvalidDataException e)
        {
            return Result<ActivityTrack, string>.Failure($"The file cannot be decompressed: {e.Message}");
        }
    }

    private static byte[] Bytes(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }
}
