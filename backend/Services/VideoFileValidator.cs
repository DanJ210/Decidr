namespace backend.Services;

public static class VideoFileValidator
{
    private static readonly byte[] MatroskaSignature = [0x1A, 0x45, 0xDF, 0xA3];

    // ISO base media files identify themselves with an "ftyp" box that follows a 4-byte size.
    private static readonly byte[] IsoBaseMediaMarker = "ftyp"u8.ToArray();

    public static async Task<bool> IsValidAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var header = new byte[12];
        if (await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken) != header.Length)
        {
            return false;
        }

        return extension.ToLowerInvariant() switch
        {
            ".mp4" or ".m4v" or ".mov" => header.AsSpan(4, 4).SequenceEqual(IsoBaseMediaMarker),
            ".webm" => header.AsSpan(0, 4).SequenceEqual(MatroskaSignature),
            _ => false,
        };
    }

    public static async Task<int?> GetDurationSecondsAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        using var content = new MemoryStream();
        await stream.CopyToAsync(content, cancellationToken);
        var durationSeconds = extension.ToLowerInvariant() switch
        {
            ".mp4" or ".m4v" or ".mov" => TryReadIsoBaseMediaDuration(content.GetBuffer().AsSpan(0, (int)content.Length)),
            ".webm" => TryReadWebmDuration(content.GetBuffer().AsSpan(0, (int)content.Length)),
            _ => null,
        };

        return durationSeconds is > 0 and <= int.MaxValue
            ? (int)Math.Ceiling(durationSeconds.Value)
            : null;
    }

    private static double? TryReadIsoBaseMediaDuration(ReadOnlySpan<byte> content)
    {
        for (var offset = 0; offset + 8 <= content.Length;)
        {
            var boxSize = ReadUInt32BigEndian(content[offset..]);
            var headerSize = 8;
            if (boxSize == 1 && offset + 16 <= content.Length)
            {
                var largeBoxSize = ReadUInt64BigEndian(content[(offset + 8)..]);
                if (largeBoxSize > int.MaxValue) return null;
                boxSize = (uint)largeBoxSize;
                headerSize = 16;
            }

            if (boxSize < headerSize || offset + boxSize > content.Length) return null;
            var boxType = content.Slice(offset + 4, 4);
            var boxContent = content.Slice(offset + headerSize, (int)boxSize - headerSize);
            if (boxType.SequenceEqual("mvhd"u8)) return TryReadMovieHeaderDuration(boxContent);
            if (boxType.SequenceEqual("moov"u8)) return TryReadIsoBaseMediaDuration(boxContent);

            offset += (int)boxSize;
        }

        return null;
    }

    private static double? TryReadMovieHeaderDuration(ReadOnlySpan<byte> content)
    {
        if (content.Length < 20) return null;
        var version = content[0];
        var timeScaleOffset = version == 1 ? 20 : 12;
        var durationOffset = version == 1 ? 24 : 16;
        if (content.Length < durationOffset + (version == 1 ? 8 : 4)) return null;

        var timeScale = ReadUInt32BigEndian(content[timeScaleOffset..]);
        var duration = version == 1
            ? ReadUInt64BigEndian(content[durationOffset..])
            : ReadUInt32BigEndian(content[durationOffset..]);
        return timeScale == 0 ? null : (double)duration / timeScale;
    }

    private static double? TryReadWebmDuration(ReadOnlySpan<byte> content)
    {
        const uint segmentElementId = 0x18538067;
        const uint infoElementId = 0x1549A966;
        const uint timecodeScaleElementId = 0x2AD7B1;
        const uint durationElementId = 0x4489;

        for (var offset = 0; offset < content.Length;)
        {
            if (!TryReadEbmlElement(content, ref offset, out var elementId, out var elementContent)) return null;
            if (elementId != segmentElementId) continue;

            var segmentOffset = 0;
            while (segmentOffset < elementContent.Length)
            {
                if (!TryReadEbmlElement(elementContent, ref segmentOffset, out var segmentId, out var infoContent)) return null;
                if (segmentId != infoElementId) continue;

                long timecodeScale = 1_000_000;
                double? duration = null;
                var infoOffset = 0;
                while (infoOffset < infoContent.Length)
                {
                    if (!TryReadEbmlElement(infoContent, ref infoOffset, out var infoId, out var valueContent)) return null;
                if (infoId == timecodeScaleElementId && valueContent.Length is > 0 and <= 8)
                {
                    timecodeScale = 0;
                        foreach (var value in valueContent) timecodeScale = (timecodeScale << 8) | value;
                }
                else if (infoId == durationElementId)
                {
                        duration = valueContent.Length switch
                    {
                            4 => BitConverter.Int32BitsToSingle((int)ReadUInt32BigEndian(valueContent)),
                            8 => BitConverter.Int64BitsToDouble((long)ReadUInt64BigEndian(valueContent)),
                        _ => null,
                    };
                }
            }

                return duration is > 0 && timecodeScale > 0
                    ? duration.Value * timecodeScale / 1_000_000_000d
                    : null;
            }
        }

        return null;
    }

    private static bool TryReadEbmlElement(ReadOnlySpan<byte> content, ref int offset, out uint elementId, out ReadOnlySpan<byte> elementContent)
    {
        elementId = 0;
        elementContent = default;
        if (offset >= content.Length) return false;

        var elementIdLength = EbmlLength(content[offset]);
        if (elementIdLength == 0 || offset + elementIdLength >= content.Length) return false;
        for (var index = 0; index < elementIdLength; index++) elementId = (elementId << 8) | content[offset + index];
        offset += elementIdLength;

        var sizeLength = EbmlLength(content[offset]);
        if (sizeLength == 0 || offset + sizeLength > content.Length) return false;
        var size = content[offset] & (byte)((1 << (8 - sizeLength)) - 1);
        for (var index = 1; index < sizeLength; index++) size = (size << 8) | content[offset + index];
        offset += sizeLength;
        if (size == (1L << (sizeLength * 7)) - 1)
        {
            elementContent = content[offset..];
            offset = content.Length;
            return true;
        }
        if (size > content.Length - offset) return false;

        elementContent = content.Slice(offset, (int)size);
        offset += (int)size;
        return true;
    }

    private static int EbmlLength(byte value)
    {
        for (var length = 1; length <= 8; length++) if ((value & (0x80 >> (length - 1))) != 0) return length;
        return 0;
    }

    private static uint ReadUInt32BigEndian(ReadOnlySpan<byte> content) =>
        ((uint)content[0] << 24) | ((uint)content[1] << 16) | ((uint)content[2] << 8) | content[3];

    private static ulong ReadUInt64BigEndian(ReadOnlySpan<byte> content) =>
        ((ulong)ReadUInt32BigEndian(content) << 32) | ReadUInt32BigEndian(content[4..]);
}
