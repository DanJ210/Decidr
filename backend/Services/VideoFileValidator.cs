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
        return await IsValidAsync(stream, extension, cancellationToken);
    }

    public static async Task<bool> IsValidAsync(
        Stream stream,
        string extension,
        CancellationToken cancellationToken)
    {
        if (stream.CanSeek) stream.Position = 0;
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
        return await GetDurationSecondsAsync(stream, extension, cancellationToken);
    }

    public static async Task<int?> GetDurationSecondsAsync(
        Stream stream,
        string extension,
        CancellationToken cancellationToken)
    {
        if (stream.CanSeek) stream.Position = 0;
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
        var movieDuration = TryReadMovieDuration(content);
        // Browser recorders emit fragmented MP4, whose mvhd duration is always zero.
        return movieDuration is > 0 ? movieDuration : TryReadFragmentedDuration(content);
    }

    private static double? TryReadMovieDuration(ReadOnlySpan<byte> content)
    {
        var boxes = new BoxReader(content);
        while (boxes.Read())
        {
            if (boxes.Type.SequenceEqual("mvhd"u8)) return TryReadMovieHeaderDuration(boxes.Content);
            if (boxes.Type.SequenceEqual("moov"u8)) return TryReadMovieDuration(boxes.Content);
        }

        return null;
    }

    private static double? TryReadFragmentedDuration(ReadOnlySpan<byte> content)
    {
        Dictionary<uint, uint>? timeScales = null;
        var longest = 0d;

        var boxes = new BoxReader(content);
        while (boxes.Read())
        {
            if (boxes.Type.SequenceEqual("moov"u8))
            {
                timeScales = ReadTrackTimeScales(boxes.Content);
            }
            else if (boxes.Type.SequenceEqual("moof"u8) && timeScales is not null)
            {
                var fragments = new BoxReader(boxes.Content);
                while (fragments.Read())
                {
                    if (!fragments.Type.SequenceEqual("traf"u8)) continue;
                    if (!TryReadTrackFragmentEnd(fragments.Content, out var trackId, out var endTicks)) continue;
                    if (!timeScales.TryGetValue(trackId, out var timeScale) || timeScale == 0) continue;

                    longest = Math.Max(longest, (double)endTicks / timeScale);
                }
            }
        }

        return longest > 0 ? longest : null;
    }

    private static Dictionary<uint, uint> ReadTrackTimeScales(ReadOnlySpan<byte> movie)
    {
        var timeScales = new Dictionary<uint, uint>();
        var boxes = new BoxReader(movie);
        while (boxes.Read())
        {
            if (!boxes.Type.SequenceEqual("trak"u8)) continue;

            uint trackId = 0;
            uint timeScale = 0;
            var trak = new BoxReader(boxes.Content);
            while (trak.Read())
            {
                if (trak.Type.SequenceEqual("tkhd"u8) && trak.Content.Length >= 20)
                {
                    var trackIdOffset = trak.Content[0] == 1 ? 20 : 12;
                    if (trak.Content.Length >= trackIdOffset + 4)
                    {
                        trackId = ReadUInt32BigEndian(trak.Content[trackIdOffset..]);
                    }
                }
                else if (trak.Type.SequenceEqual("mdia"u8))
                {
                    var mdia = new BoxReader(trak.Content);
                    while (mdia.Read())
                    {
                        if (!mdia.Type.SequenceEqual("mdhd"u8) || mdia.Content.Length < 20) continue;
                        var timeScaleOffset = mdia.Content[0] == 1 ? 20 : 12;
                        if (mdia.Content.Length >= timeScaleOffset + 4)
                        {
                            timeScale = ReadUInt32BigEndian(mdia.Content[timeScaleOffset..]);
                        }
                    }
                }
            }

            if (trackId != 0 && timeScale != 0) timeScales[trackId] = timeScale;
        }

        return timeScales;
    }

    private static bool TryReadTrackFragmentEnd(ReadOnlySpan<byte> fragment, out uint trackId, out ulong endTicks)
    {
        trackId = 0;
        endTicks = 0;
        ulong baseDecodeTime = 0;
        uint defaultSampleDuration = 0;
        ulong sampleTicks = 0;

        var boxes = new BoxReader(fragment);
        while (boxes.Read())
        {
            if (boxes.Type.SequenceEqual("tfhd"u8))
            {
                if (boxes.Content.Length < 8) return false;
                trackId = ReadUInt32BigEndian(boxes.Content[4..]);
                var flags = (ReadUInt32BigEndian(boxes.Content) & 0x00FFFFFF);
                var offset = 8;
                if ((flags & 0x000001) != 0) offset += 8;
                if ((flags & 0x000002) != 0) offset += 4;
                if ((flags & 0x000008) != 0 && boxes.Content.Length >= offset + 4)
                {
                    defaultSampleDuration = ReadUInt32BigEndian(boxes.Content[offset..]);
                }
            }
            else if (boxes.Type.SequenceEqual("tfdt"u8) && boxes.Content.Length >= 8)
            {
                baseDecodeTime = boxes.Content[0] == 1
                    ? ReadUInt64BigEndian(boxes.Content[4..])
                    : ReadUInt32BigEndian(boxes.Content[4..]);
            }
            else if (boxes.Type.SequenceEqual("trun"u8) && boxes.Content.Length >= 8)
            {
                sampleTicks += ReadTrackRunTicks(boxes.Content, defaultSampleDuration);
            }
        }

        endTicks = baseDecodeTime + sampleTicks;
        return trackId != 0 && endTicks > 0;
    }

    private static ulong ReadTrackRunTicks(ReadOnlySpan<byte> run, uint defaultSampleDuration)
    {
        var flags = ReadUInt32BigEndian(run) & 0x00FFFFFF;
        var sampleCount = ReadUInt32BigEndian(run[4..]);
        if ((flags & 0x000100) == 0) return (ulong)sampleCount * defaultSampleDuration;

        var offset = 8;
        if ((flags & 0x000001) != 0) offset += 4;
        if ((flags & 0x000004) != 0) offset += 4;

        var entrySize = 4
            + ((flags & 0x000200) != 0 ? 4 : 0)
            + ((flags & 0x000400) != 0 ? 4 : 0)
            + ((flags & 0x000800) != 0 ? 4 : 0);

        ulong ticks = 0;
        for (var sample = 0u; sample < sampleCount; sample++)
        {
            if (offset + 4 > run.Length) break;
            ticks += ReadUInt32BigEndian(run[offset..]);
            offset += entrySize;
        }

        return ticks;
    }

    private ref struct BoxReader(ReadOnlySpan<byte> content)
    {
        private readonly ReadOnlySpan<byte> _content = content;
        private int _offset = 0;

        public ReadOnlySpan<byte> Type { get; private set; } = default;
        public ReadOnlySpan<byte> Content { get; private set; } = default;

        public bool Read()
        {
            if (_offset + 8 > _content.Length) return false;

            long boxSize = ReadUInt32BigEndian(_content[_offset..]);
            var headerSize = 8;
            if (boxSize == 1)
            {
                if (_offset + 16 > _content.Length) return false;
                var largeSize = ReadUInt64BigEndian(_content[(_offset + 8)..]);
                if (largeSize > int.MaxValue) return false;
                boxSize = (long)largeSize;
                headerSize = 16;
            }
            else if (boxSize == 0)
            {
                // A zero size means the box runs to the end of the container.
                boxSize = _content.Length - _offset;
            }

            if (boxSize < headerSize || _offset + boxSize > _content.Length) return false;

            Type = _content.Slice(_offset + 4, 4);
            Content = _content.Slice(_offset + headerSize, (int)boxSize - headerSize);
            _offset += (int)boxSize;
            return true;
        }
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

        for (var offset = 0; offset < content.Length;)
        {
            if (!TryReadEbmlElement(content, ref offset, out var elementId, out var segment)) return null;
            if (elementId != segmentElementId) continue;

            var timecodeScale = ReadWebmTimecodeScale(segment);
            var duration = ReadWebmInfoDuration(segment);
            if (duration is null or <= 0)
            {
                // MediaRecorder writes a live segment with no Duration, so measure the clusters.
                var ticks = ReadWebmClusterEndTicks(segment);
                duration = ticks > 0 ? ticks : null;
            }

            return duration is > 0 && timecodeScale > 0
                ? duration.Value * timecodeScale / 1_000_000_000d
                : null;
        }

        return null;
    }

    private static long ReadWebmTimecodeScale(ReadOnlySpan<byte> segment)
    {
        const uint infoElementId = 0x1549A966;
        const uint timecodeScaleElementId = 0x2AD7B1;

        var offset = 0;
        while (offset < segment.Length)
        {
            if (!TryReadEbmlElement(segment, ref offset, out var elementId, out var info)) break;
            if (elementId != infoElementId) continue;

            var infoOffset = 0;
            while (infoOffset < info.Length)
            {
                if (!TryReadEbmlElement(info, ref infoOffset, out var infoId, out var value)) break;
                if (infoId != timecodeScaleElementId || value.Length is <= 0 or > 8) continue;

                long timecodeScale = 0;
                foreach (var part in value) timecodeScale = (timecodeScale << 8) | part;
                if (timecodeScale > 0) return timecodeScale;
            }
        }

        return 1_000_000;
    }

    private static double? ReadWebmInfoDuration(ReadOnlySpan<byte> segment)
    {
        const uint infoElementId = 0x1549A966;
        const uint durationElementId = 0x4489;

        var offset = 0;
        while (offset < segment.Length)
        {
            if (!TryReadEbmlElement(segment, ref offset, out var elementId, out var info)) break;
            if (elementId != infoElementId) continue;

            var infoOffset = 0;
            while (infoOffset < info.Length)
            {
                if (!TryReadEbmlElement(info, ref infoOffset, out var infoId, out var value)) break;
                if (infoId != durationElementId) continue;

                return value.Length switch
                {
                    4 => BitConverter.Int32BitsToSingle((int)ReadUInt32BigEndian(value)),
                    8 => BitConverter.Int64BitsToDouble((long)ReadUInt64BigEndian(value)),
                    _ => null,
                };
            }
        }

        return null;
    }

    private static double ReadWebmClusterEndTicks(ReadOnlySpan<byte> segment)
    {
        const uint clusterElementId = 0x1F43B675;
        const uint timecodeElementId = 0xE7;
        const uint simpleBlockElementId = 0xA3;
        const uint blockGroupElementId = 0xA0;
        const uint blockElementId = 0xA1;

        var end = 0d;
        var offset = 0;
        while (offset < segment.Length)
        {
            if (!TryReadEbmlElement(segment, ref offset, out var elementId, out var cluster)) break;
            if (elementId != clusterElementId) continue;

            double clusterTimecode = 0;
            double lastBlockOffset = 0;
            var clusterOffset = 0;
            while (clusterOffset < cluster.Length)
            {
                if (!TryReadEbmlElement(cluster, ref clusterOffset, out var clusterId, out var value)) break;

                if (clusterId == timecodeElementId && value.Length is > 0 and <= 8)
                {
                    long timecode = 0;
                    foreach (var part in value) timecode = (timecode << 8) | part;
                    clusterTimecode = timecode;
                }
                else if (clusterId == simpleBlockElementId)
                {
                    lastBlockOffset = Math.Max(lastBlockOffset, ReadWebmBlockTimecode(value));
                }
                else if (clusterId == blockGroupElementId)
                {
                    var groupOffset = 0;
                    while (groupOffset < value.Length)
                    {
                        if (!TryReadEbmlElement(value, ref groupOffset, out var groupId, out var block)) break;
                        if (groupId != blockElementId) continue;
                        lastBlockOffset = Math.Max(lastBlockOffset, ReadWebmBlockTimecode(block));
                    }
                }
            }

            end = Math.Max(end, clusterTimecode + lastBlockOffset);
        }

        return end;
    }

    private static double ReadWebmBlockTimecode(ReadOnlySpan<byte> block)
    {
        if (block.Length == 0) return 0;
        var trackNumberLength = EbmlLength(block[0]);
        if (trackNumberLength == 0 || block.Length < trackNumberLength + 2) return 0;

        var relative = (short)((block[trackNumberLength] << 8) | block[trackNumberLength + 1]);
        return relative > 0 ? relative : 0;
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
