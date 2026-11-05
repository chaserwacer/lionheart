using System.Buffers.Binary;
using lionheart.Functional;

namespace lionheart.Services.Strava.Files;

/// <summary>
/// Reads the <c>record</c> messages out of a FIT activity file — the samples a device took every
/// second or so — and nothing else.
/// </summary>
/// <remarks>
/// Written here rather than taken from Garmin's SDK because the archive needs eight fields of one
/// message, and the SDK is a generated profile of every message and field FIT defines, to be kept
/// current for a reader that ignores almost all of it. The layout this needs is small and stable: a
/// header, then definition messages saying how each later data message is laid out (field number,
/// size, base type, byte order), developer fields skipped by their declared size, and the
/// compressed-timestamp header that carries the time as five bits against the last full timestamp.
/// A reading the device marked invalid (all bits set) is absent. The checksum is not verified: a
/// damaged file shows as a layout that does not fit, and is refused for that.
/// </remarks>
internal static class FitRecords
{
    private const ushort RecordMessage = 20;
    private const byte TimestampField = 253;

    /// <summary>FIT counts seconds from the last day of 1989.</summary>
    private static readonly DateTimeOffset Epoch = new(1989, 12, 31, 0, 0, 0, TimeSpan.Zero);

    /// <summary>A semicircle is 180° over 2³¹.</summary>
    private const double DegreesPerSemicircle = 180.0 / 2147483648.0;

    /// <summary>The record samples in the file, oldest first, or why it cannot be read as FIT.</summary>
    public static Result<IReadOnlyList<TrackSample>, string> Read(byte[] file)
    {
        try
        {
            return Result<IReadOnlyList<TrackSample>, string>.Success(Decode(file));
        }
        catch (InvalidDataException e)
        {
            return Result<IReadOnlyList<TrackSample>, string>.Failure($"The FIT file {e.Message}.");
        }
    }

    private static IReadOnlyList<TrackSample> Decode(ReadOnlySpan<byte> file)
    {
        Require(file.Length >= 12 && file[0] is 12 or 14 && file.Length >= file[0], "is too short to hold a FIT header");
        Require(file.Slice(8, 4).SequenceEqual(".FIT"u8), "does not carry FIT's signature");

        var headerSize = file[0];
        var dataSize = BinaryPrimitives.ReadUInt32LittleEndian(file.Slice(4, 4));
        Require(headerSize + dataSize <= file.Length, "declares more data than it holds");

        return Messages(file.Slice(headerSize, (int)dataSize));
    }

    /// <summary>Walks the messages, keeping each local type's latest definition and the last full timestamp.</summary>
    private static IReadOnlyList<TrackSample> Messages(ReadOnlySpan<byte> data)
    {
        var definitions = new Definition?[16];
        var records = new List<TrackSample>();
        uint? lastTimestamp = null;
        var position = 0;

        while (position < data.Length)
        {
            var header = data[position++];

            if ((header & 0x80) != 0)
            {
                var compressed = Defined(definitions, (header >> 5) & 0x03);
                var last = lastTimestamp ?? throw new InvalidDataException("compresses a timestamp before any full one");
                var offset = (uint)(header & 0x1F);
                var timestamp = (last & ~0x1Fu) + offset + (offset >= (last & 0x1F) ? 0u : 0x20u);
                var content = Content(data, position, compressed);

                lastTimestamp = timestamp;
                if (compressed.Global == RecordMessage) records.Add(Record(compressed, content, timestamp));
                position += compressed.Size;
            }
            else if ((header & 0x40) != 0)
            {
                definitions[header & 0x0F] = Define(data, ref position, hasDeveloperFields: (header & 0x20) != 0);
            }
            else
            {
                var defined = Defined(definitions, header & 0x0F);
                var content = Content(data, position, defined);

                if (defined.Read(content, TimestampField) is { } full) lastTimestamp = (uint)full;
                if (defined.Global == RecordMessage && lastTimestamp is { } at) records.Add(Record(defined, content, at));
                position += defined.Size;
            }
        }

        return records;
    }

    private static Definition Defined(Definition?[] definitions, int local) =>
        definitions[local] ?? throw new InvalidDataException($"uses local message {local} before defining it");

    private static ReadOnlySpan<byte> Content(ReadOnlySpan<byte> data, int position, Definition definition)
    {
        Require(position + definition.Size <= data.Length, "ends inside a message");
        return data.Slice(position, definition.Size);
    }

    private static Definition Define(ReadOnlySpan<byte> data, ref int position, bool hasDeveloperFields)
    {
        Require(position + 5 <= data.Length, "ends inside a definition");

        var bigEndian = data[position + 1] == 1;
        var globalBytes = data.Slice(position + 2, 2);
        var global = bigEndian ? BinaryPrimitives.ReadUInt16BigEndian(globalBytes) : BinaryPrimitives.ReadUInt16LittleEndian(globalBytes);
        var count = data[position + 4];
        position += 5;

        Require(position + count * 3 <= data.Length, "ends inside a definition");
        var fields = new List<Field>(count);
        var size = 0;
        for (var i = 0; i < count; i++, position += 3)
        {
            fields.Add(new Field(data[position], size, data[position + 1], data[position + 2]));
            size += data[position + 1];
        }

        if (hasDeveloperFields)
        {
            Require(position < data.Length, "ends inside a definition");
            var developerCount = data[position++];
            Require(position + developerCount * 3 <= data.Length, "ends inside a definition");
            for (var i = 0; i < developerCount; i++, position += 3) size += data[position + 1];
        }

        return new Definition(global, bigEndian, fields, size);
    }

    private static TrackSample Record(Definition definition, ReadOnlySpan<byte> content, uint timestamp)
    {
        var latitude = definition.Read(content, 0);
        var longitude = definition.Read(content, 1);
        var hasFix = latitude is not null && longitude is not null;

        return new TrackSample(
            Epoch.AddSeconds(timestamp),
            hasFix ? latitude * DegreesPerSemicircle : null,
            hasFix ? longitude * DegreesPerSemicircle : null,
            Scaled(definition.Read(content, 78) ?? definition.Read(content, 2), scale: 5, offset: 500),
            (int?)definition.Read(content, 3),
            (int?)definition.Read(content, 4),
            (int?)definition.Read(content, 7),
            Scaled(definition.Read(content, 73) ?? definition.Read(content, 6), scale: 1000, offset: 0));
    }

    private static double? Scaled(long? raw, double scale, double offset) => raw is { } value ? value / scale - offset : null;

    private static void Require(bool holds, string otherwise)
    {
        if (!holds) throw new InvalidDataException(otherwise);
    }

    /// <summary>One field of a definition: its number, where it sits in a data message, its size and base type.</summary>
    private sealed record Field(byte Number, int Offset, byte Size, byte BaseType);

    /// <summary>How a local message type's data messages are laid out, as its definition message said.</summary>
    private sealed record Definition(ushort Global, bool BigEndian, IReadOnlyList<Field> Fields, int Size)
    {
        /// <summary>
        /// A field's value as an integer of its base type, or nothing where the message lacks the
        /// field, the device marked it invalid, or its size is not one value of that type.
        /// </summary>
        public long? Read(ReadOnlySpan<byte> content, byte number)
        {
            if (Fields.FirstOrDefault(field => field.Number == number) is not { } field) return null;

            var bytes = content.Slice(field.Offset, field.Size);
            return (field.BaseType & 0x1F, field.Size) switch
            {
                (0x00 or 0x02 or 0x0A, 1) => bytes[0] == byte.MaxValue ? null : bytes[0],
                (0x01, 1) => (sbyte)bytes[0] == sbyte.MaxValue ? null : (sbyte)bytes[0],
                (0x04 or 0x0B, 2) => Unsigned16(bytes) is var u16 && u16 == ushort.MaxValue ? null : u16,
                (0x03, 2) => Signed16(bytes) is var s16 && s16 == short.MaxValue ? null : s16,
                (0x06 or 0x0C, 4) => Unsigned32(bytes) is var u32 && u32 == uint.MaxValue ? null : u32,
                (0x05, 4) => Signed32(bytes) is var s32 && s32 == int.MaxValue ? null : s32,
                _ => null,
            };
        }

        private ushort Unsigned16(ReadOnlySpan<byte> bytes) =>
            BigEndian ? BinaryPrimitives.ReadUInt16BigEndian(bytes) : BinaryPrimitives.ReadUInt16LittleEndian(bytes);

        private short Signed16(ReadOnlySpan<byte> bytes) =>
            BigEndian ? BinaryPrimitives.ReadInt16BigEndian(bytes) : BinaryPrimitives.ReadInt16LittleEndian(bytes);

        private uint Unsigned32(ReadOnlySpan<byte> bytes) =>
            BigEndian ? BinaryPrimitives.ReadUInt32BigEndian(bytes) : BinaryPrimitives.ReadUInt32LittleEndian(bytes);

        private int Signed32(ReadOnlySpan<byte> bytes) =>
            BigEndian ? BinaryPrimitives.ReadInt32BigEndian(bytes) : BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }
}
