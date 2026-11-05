using lionheart.Functional;
using lionheart.Services.Strava.Files;
using lionheart.Tests.Support;
using static lionheart.Tests.Strava.FitFile;

namespace lionheart.Tests.Strava;

/// <summary>
/// Reading the samples out of a FIT activity file: the fields the import needs, in the units it
/// holds them in, past everything the protocol allows that it does not need.
/// </summary>
public class FitRecordsTests
{
    private static readonly DateTimeOffset Start = new(2025, 3, 14, 10, 0, 0, TimeSpan.Zero);

    /// <summary>A record message: timestamp, position, enhanced altitude, heart rate, cadence, power, enhanced speed.</summary>
    private static readonly (byte, byte, byte)[] RecordFields =
    [
        (253, 4, 0x86), (0, 4, 0x85), (1, 4, 0x85), (78, 4, 0x86), (3, 1, 0x02), (4, 1, 0x02), (7, 2, 0x84), (73, 4, 0x86),
    ];

    private static byte[] RecordAt(
        DateTimeOffset at, double latitude, double longitude, double altitude, byte bpm, byte cadence, ushort watts, double speed) =>
    [
        .. U32(Timestamp(at)),
        .. S32(Semicircles(latitude)),
        .. S32(Semicircles(longitude)),
        .. U32((uint)Math.Round((altitude + 500) * 5)),
        bpm,
        cadence,
        (byte)(watts & 0xFF), (byte)(watts >> 8),
        .. U32((uint)Math.Round(speed * 1000)),
    ];

    [Fact]
    public void A_record_reads_as_its_time_place_altitude_and_readings_in_their_own_units()
    {
        var file = new FitFile()
            .Define(0, 20, RecordFields)
            .Data(0, RecordAt(Start, AHealthRun.Latitude, AHealthRun.Longitude, 412.4, 141, 88, 205, 7.25))
            .Bytes();

        var sample = Assert.Single(ResultAssert.Succeeded(FitRecords.Read(file)));

        Assert.Equal(Start, sample.At);
        Assert.Equal(AHealthRun.Latitude, sample.Latitude!.Value, precision: 6);
        Assert.Equal(AHealthRun.Longitude, sample.Longitude!.Value, precision: 6);
        Assert.Equal(412.4, sample.Altitude!.Value, precision: 1);
        Assert.Equal((141, 88.0, 205.0), (sample.HeartRate, sample.Cadence, sample.Power));
        Assert.Equal(7.25, sample.Speed!.Value, precision: 3);
    }

    [Fact]
    public void A_reading_the_device_marked_invalid_is_absent_and_a_record_without_a_fix_has_no_position()
    {
        var file = new FitFile()
            .Define(0, 20, [(253, 4, 0x86), (0, 4, 0x85), (1, 4, 0x85), (3, 1, 0x02)])
            .Data(0, [.. U32(Timestamp(Start)), .. S32(int.MaxValue), .. S32(int.MaxValue), 0xFF])
            .Bytes();

        var sample = Assert.Single(ResultAssert.Succeeded(FitRecords.Read(file)));

        Assert.Equal((null, null, null), (sample.Latitude, sample.Longitude, sample.HeartRate));
    }

    [Fact]
    public void A_compressed_timestamp_counts_on_from_the_last_full_one_across_its_rollover()
    {
        // The full timestamp sits two seconds before its low five bits roll over, and the next sample five seconds on.
        var first = (Timestamp(Start) & ~0x1Fu) | 30;
        var at = Start.AddSeconds((double)first - Timestamp(Start));
        var file = new FitFile()
            .Define(0, 20, [(253, 4, 0x86), (3, 1, 0x02)])
            .Define(1, 20, [(3, 1, 0x02)])
            .Data(0, [.. U32(first), 120])
            .Compressed(1, (byte)((first + 5) & 0x1F), 125)
            .Bytes();

        var samples = ResultAssert.Succeeded(FitRecords.Read(file));

        Assert.Equal([(at, 120), (at.AddSeconds(5), 125)], samples.Select(sample => (sample.At, sample.HeartRate!.Value)));
    }

    [Fact]
    public void Other_messages_developer_fields_and_a_big_endian_layout_are_read_past()
    {
        var file = new FitFile()
            .Define(0, 0, [(4, 4, 0x86)])
            .Data(0, U32(Timestamp(Start)))
            .Define(1, 20, [(253, 4, 0x86), (3, 1, 0x02)], bigEndian: true, developerFieldSizes: [2, 1])
            .Data(1, [.. U32(Timestamp(Start.AddSeconds(1)), bigEndian: true), 133, 0xAA, 0xBB, 0xCC])
            .Bytes();

        var sample = Assert.Single(ResultAssert.Succeeded(FitRecords.Read(file)));

        Assert.Equal((Start.AddSeconds(1), 133), (sample.At, sample.HeartRate!.Value));
    }

    [Fact]
    public void A_file_without_FITs_signature_is_refused_saying_so()
    {
        var file = new FitFile().Define(0, 20, RecordFields).Bytes();
        file[8] = (byte)'X';

        Assert.Equal("The FIT file does not carry FIT's signature.", Refusal(file));
    }

    [Fact]
    public void A_data_message_of_a_type_never_defined_is_refused()
    {
        var file = new FitFile().Data(3, 1, 2, 3).Bytes();

        Assert.Equal("The FIT file uses local message 3 before defining it.", Refusal(file));
    }

    [Fact]
    public void A_file_cut_off_inside_a_message_is_refused_rather_than_read_short()
    {
        var whole = new FitFile()
            .Define(0, 20, RecordFields)
            .Data(0, RecordAt(Start, AHealthRun.Latitude, AHealthRun.Longitude, 400, 140, 80, 200, 3))
            .Bytes();
        var cut = whole[..^8];
        BitConverterHeader(cut, dataSize: cut.Length - 14);

        Assert.Equal("The FIT file ends inside a message.", Refusal(cut));
    }

    private static string Refusal(byte[] file) =>
        Assert.IsType<Failure<IReadOnlyList<TrackSample>, string>>(FitRecords.Read(file)).Problem;

    private static void BitConverterHeader(byte[] file, int dataSize) =>
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(4), (uint)dataSize);
}
