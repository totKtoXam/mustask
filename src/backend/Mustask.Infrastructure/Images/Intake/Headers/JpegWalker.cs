using System.Buffers.Binary;

namespace Mustask.Infrastructure.Images.Intake.Headers;

/// <summary>One marker segment of a JPEG: <see cref="Start"/> at its 0xFF, <see cref="HeaderEnd"/> after its length-counted part, <see cref="End"/> after its entropy-coded data (SOS only).</summary>
internal readonly record struct JpegSegment(byte Marker, int Start, int HeaderEnd, int End)
{
    public bool IsStartOfScan => Marker == 0xDA;

    public bool IsEndOfImage => Marker == 0xD9;

    /// <summary>SOF0..SOF15 except DHT (C4), JPG (C8) and DAC (CC).</summary>
    public bool IsStartOfFrame => Marker is >= 0xC0 and <= 0xCF && Marker is not (0xC4 or 0xC8 or 0xCC);

    /// <summary>The length-counted payload, after the two length bytes.</summary>
    public ReadOnlySpan<byte> Payload(ReadOnlySpan<byte> data) => data[(Start + 4)..HeaderEnd];
}

/// <summary>
/// Iterative marker walk shared by the JPEG reader and stripper (plan-backend § 4.1): every length is checked against the
/// bytes left, the entropy-coded data after a scan header is skipped by one linear scan (<c>FF 00</c> stuffing, RSTn, fill
/// bytes), a standalone marker outside a scan is a refusal. Nothing allocates in proportion to a declared size.
/// </summary>
internal static class JpegWalker
{
    /// <summary>The next segment from <paramref name="position"/>, or <c>false</c> for a malformed file. EOI is returned as a segment of its own.</summary>
    public static bool TryNext(ReadOnlySpan<byte> data, ref int position, out JpegSegment segment)
    {
        segment = default;
        var start = position;

        if (start >= data.Length || data[start] != 0xFF)
        {
            return false;
        }

        var cursor = start;
        while (cursor < data.Length && data[cursor] == 0xFF)
        {
            cursor++;
        }

        if (cursor >= data.Length)
        {
            return false;
        }

        var marker = data[cursor++];

        if (marker == 0xD9)
        {
            segment = new JpegSegment(marker, start, cursor, cursor);
            position = cursor;

            return true;
        }

        // Standalone markers (TEM, RSTn, SOI) and a stuffed zero have no place between segments.
        if (marker is 0x00 or 0x01 or (>= 0xD0 and <= 0xD8))
        {
            return false;
        }

        if (cursor + 2 > data.Length)
        {
            return false;
        }

        var length = BinaryPrimitives.ReadUInt16BigEndian(data[cursor..]);
        if (length < 2 || cursor + length > data.Length)
        {
            return false;
        }

        // The segment's own start is the first 0xFF; the length bytes sit right after the marker byte.
        var headerEnd = cursor + length;
        var end = headerEnd;

        if (marker == 0xDA)
        {
            if (!TrySkipEntropyData(data, headerEnd, out end))
            {
                return false;
            }
        }

        // Payload() assumes Start + 4 is the first payload byte: normalise Start to the marker's own 0xFF.
        segment = new JpegSegment(marker, cursor - 2, headerEnd, end);
        position = end;

        return true;
    }

    private static bool TrySkipEntropyData(ReadOnlySpan<byte> data, int from, out int end)
    {
        var position = from;

        while (true)
        {
            var offset = data[position..].IndexOf((byte)0xFF);
            if (offset < 0)
            {
                end = 0;
                return false;
            }

            position += offset;
            if (position + 1 >= data.Length)
            {
                end = 0;
                return false;
            }

            var next = data[position + 1];

            if (next == 0x00 || next is >= 0xD0 and <= 0xD7)
            {
                position += 2;
                continue;
            }

            if (next == 0xFF)
            {
                position++;
                continue;
            }

            end = position;
            return true;
        }
    }
}
