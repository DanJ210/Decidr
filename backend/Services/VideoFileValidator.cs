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
        Dictionary<uint, TrackDefaults>? trackDefaults = null;
        var previousDecodeEnds = new Dictionary<uint, ulong>();
        // Safari stamps the first fragment with a capture-clock decode time, so measure from each track's first fragment.
        var firstDecodeStarts = new Dictionary<uint, ulong>();
        var longest = 0d;

        var boxes = new BoxReader(content);
        while (boxes.Read())
        {
            if (boxes.Type.SequenceEqual("moov"u8))
            {
                trackDefaults = ReadTrackDefaults(boxes.Content);
            }
            else if (boxes.Type.SequenceEqual("moof"u8) && trackDefaults is not null)
            {
                var fragments = new BoxReader(boxes.Content);
                while (fragments.Read())
                {
                    if (!fragments.Type.SequenceEqual("traf"u8)) continue;
                    if (!TryReadTrackFragmentEnd(
                        fragments.Content,
                        trackDefaults,
                        previousDecodeEnds,
                        out var trackId,
                        out var startTicks,
                        out var endTicks,
                        out var malformed))
                    {
                        if (malformed) return null;
                        continue;
                    }

                    previousDecodeEnds[trackId] = endTicks;
                    var firstStart = firstDecodeStarts.TryGetValue(trackId, out var knownStart)
                        ? Math.Min(knownStart, startTicks)
                        : startTicks;
                    firstDecodeStarts[trackId] = firstStart;
                    longest = Math.Max(longest, (double)(endTicks - firstStart) / trackDefaults[trackId].TimeScale);
                }
            }
        }

        return longest > 0 ? longest : null;
    }

    private static Dictionary<uint, TrackDefaults> ReadTrackDefaults(ReadOnlySpan<byte> movie)
    {
        var timeScales = new Dictionary<uint, uint>();
        var defaultSampleDurations = new Dictionary<uint, uint>();
        var boxes = new BoxReader(movie);
        while (boxes.Read())
        {
            if (boxes.Type.SequenceEqual("mvex"u8))
            {
                var mvex = new BoxReader(boxes.Content);
                while (mvex.Read())
                {
                    if (!mvex.Type.SequenceEqual("trex"u8) || mvex.Content.Length < 24) continue;
                    var trexTrackId = ReadUInt32BigEndian(mvex.Content[4..]);
                    var defaultSampleDuration = ReadUInt32BigEndian(mvex.Content[12..]);
                    if (trexTrackId != 0) defaultSampleDurations[trexTrackId] = defaultSampleDuration;
                }

                continue;
            }

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

        return timeScales.ToDictionary(
            pair => pair.Key,
            pair => new TrackDefaults(pair.Value, defaultSampleDurations.GetValueOrDefault(pair.Key)));
    }

    private static bool TryReadTrackFragmentEnd(
        ReadOnlySpan<byte> fragment,
        IReadOnlyDictionary<uint, TrackDefaults> trackDefaults,
        IReadOnlyDictionary<uint, ulong> previousDecodeEnds,
        out uint trackId,
        out ulong startTicks,
        out ulong endTicks,
        out bool malformed)
    {
        trackId = 0;
        startTicks = 0;
        endTicks = 0;
        malformed = false;
        ulong? explicitBaseDecodeTime = null;
        uint defaultSampleDuration = 0;
        ulong sampleTicks = 0;
        var hasTrackRun = false;

        var boxes = new BoxReader(fragment);
        while (boxes.Read())
        {
            if (boxes.Type.SequenceEqual("tfhd"u8))
            {
                if (boxes.Content.Length < 8)
                {
                    malformed = true;
                    return false;
                }

                trackId = ReadUInt32BigEndian(boxes.Content[4..]);
                if (trackDefaults.TryGetValue(trackId, out var track))
                {
                    defaultSampleDuration = track.DefaultSampleDuration;
                }

                var flags = (ReadUInt32BigEndian(boxes.Content) & 0x00FFFFFF);
                var offset = 8;
                if ((flags & 0x000001) != 0) offset += 8;
                if ((flags & 0x000002) != 0) offset += 4;
                if ((flags & 0x000008) != 0)
                {
                    if (boxes.Content.Length < offset + 4)
                    {
                        malformed = true;
                        return false;
                    }

                    defaultSampleDuration = ReadUInt32BigEndian(boxes.Content[offset..]);
                }
            }
            else if (boxes.Type.SequenceEqual("tfdt"u8))
            {
                var version = boxes.Content.Length > 0 ? boxes.Content[0] : byte.MaxValue;
                if ((version == 0 && boxes.Content.Length < 8)
                    || (version == 1 && boxes.Content.Length < 12)
                    || version > 1)
                {
                    malformed = true;
                    return false;
                }

                explicitBaseDecodeTime = version == 1
                    ? ReadUInt64BigEndian(boxes.Content[4..])
                    : ReadUInt32BigEndian(boxes.Content[4..]);
            }
            else if (boxes.Type.SequenceEqual("trun"u8))
            {
                hasTrackRun = true;
                if (!TryReadTrackRunTicks(boxes.Content, defaultSampleDuration, out var runTicks)
                    || ulong.MaxValue - sampleTicks < runTicks)
                {
                    malformed = true;
                    return false;
                }

                sampleTicks += runTicks;
            }
        }

        if (trackId == 0 || !trackDefaults.TryGetValue(trackId, out var defaults)) return false;

        if (hasTrackRun && sampleTicks == 0 && defaultSampleDuration == 0) return false;
        var baseDecodeTime = explicitBaseDecodeTime ?? previousDecodeEnds.GetValueOrDefault(trackId);
        if (ulong.MaxValue - baseDecodeTime < sampleTicks)
        {
            malformed = true;
            return false;
        }

        startTicks = baseDecodeTime;
        endTicks = baseDecodeTime + sampleTicks;
        return endTicks > 0;
    }

    private static bool TryReadTrackRunTicks(ReadOnlySpan<byte> run, uint defaultSampleDuration, out ulong ticks)
    {
        ticks = 0;
        if (run.Length < 8) return false;

        var flags = ReadUInt32BigEndian(run) & 0x00FFFFFF;
        var sampleCount = ReadUInt32BigEndian(run[4..]);
        if ((flags & 0x000100) == 0)
        {
            if (defaultSampleDuration == 0) return sampleCount == 0;
            ticks = (ulong)sampleCount * defaultSampleDuration;
            return true;
        }

        var offset = 8;
        if ((flags & 0x000001) != 0) offset += 4;
        if ((flags & 0x000004) != 0) offset += 4;

        var entrySize = 4
            + ((flags & 0x000200) != 0 ? 4 : 0)
            + ((flags & 0x000400) != 0 ? 4 : 0)
            + ((flags & 0x000800) != 0 ? 4 : 0);

        for (var sample = 0u; sample < sampleCount; sample++)
        {
            if (offset + entrySize > run.Length) return false;
            var sampleTicks = ReadUInt32BigEndian(run[offset..]);
            if (ulong.MaxValue - ticks < sampleTicks) return false;
            ticks += sampleTicks;
            offset += entrySize;
        }

        return true;
    }

    private readonly record struct TrackDefaults(uint TimeScale, uint DefaultSampleDuration);

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
                var ticks = ReadWebmClusterEndTicks(segment, timecodeScale);
                duration = ticks is > 0 ? ticks : null;
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

    private static double? ReadWebmClusterEndTicks(ReadOnlySpan<byte> segment, long timecodeScale)
    {
        const uint clusterElementId = 0x1F43B675;
        const uint timecodeElementId = 0xE7;
        const uint simpleBlockElementId = 0xA3;
        const uint blockGroupElementId = 0xA0;
        const uint blockElementId = 0xA1;

        var videoTrackDurations = ReadWebmVideoTrackDurations(segment);
        var foundCluster = false;
        var foundVideoBlock = false;
        var end = 0d;
        var offset = 0;
        while (offset < segment.Length)
        {
            if (!TryReadEbmlElement(segment, ref offset, out var elementId, out var cluster)) return null;
            if (elementId != clusterElementId) continue;

            foundCluster = true;
            double? clusterTimecode = null;
            var clusterEnd = 0d;
            var clusterOffset = 0;
            while (clusterOffset < cluster.Length)
            {
                if (!TryReadEbmlElement(cluster, ref clusterOffset, out var clusterId, out var value)) return null;

                if (clusterId == timecodeElementId && value.Length is > 0 and <= 8)
                {
                    ulong timecode = 0;
                    foreach (var part in value) timecode = (timecode << 8) | part;
                    clusterTimecode = timecode;
                }
                else if (clusterId == simpleBlockElementId)
                {
                    if (!TryReadWebmBlock(value, out var trackNumber, out var relativeTimecode, out var frameCount))
                    {
                        return null;
                    }

                    if (!videoTrackDurations.ContainsKey(trackNumber)) continue;
                    if (!TryGetWebmBlockDuration(trackNumber, frameCount, null, videoTrackDurations, timecodeScale, out var blockDuration))
                    {
                        return null;
                    }

                    foundVideoBlock = true;
                    clusterEnd = Math.Max(clusterEnd, relativeTimecode + blockDuration);
                }
                else if (clusterId == blockGroupElementId)
                {
                    var groupOffset = 0;
                    var hasBlock = false;
                    uint trackNumber = 0;
                    short relativeTimecode = 0;
                    var frameCount = 0;
                    ulong? blockDuration = null;
                    while (groupOffset < value.Length)
                    {
                        if (!TryReadEbmlElement(value, ref groupOffset, out var groupId, out var groupContent)) return null;
                        if (groupId == blockElementId)
                        {
                            if (!TryReadWebmBlock(groupContent, out trackNumber, out relativeTimecode, out frameCount))
                            {
                                return null;
                            }

                            hasBlock = true;
                        }
                        else if (groupId == 0x9B && TryReadWebmUnsigned(groupContent, out var ticks))
                        {
                            blockDuration = ticks;
                        }
                    }

                    if (!hasBlock) continue;
                    if (!videoTrackDurations.ContainsKey(trackNumber)) continue;
                    if (!TryGetWebmBlockDuration(trackNumber, frameCount, blockDuration, videoTrackDurations, timecodeScale, out var durationTicks))
                    {
                        return null;
                    }

                    foundVideoBlock = true;
                    clusterEnd = Math.Max(clusterEnd, relativeTimecode + durationTicks);
                }
            }

            if (clusterTimecode is null) return null;
            end = Math.Max(end, Math.Max(0, clusterTimecode.Value + clusterEnd));
        }

        return foundCluster && foundVideoBlock ? end : null;
    }

    private static Dictionary<uint, ulong> ReadWebmVideoTrackDurations(ReadOnlySpan<byte> segment)
    {
        const uint tracksElementId = 0x1654AE6B;
        const uint trackEntryElementId = 0xAE;
        const uint trackNumberElementId = 0xD7;
        const uint trackTypeElementId = 0x83;
        const uint defaultDurationElementId = 0x23E383;
        var durations = new Dictionary<uint, ulong>();

        var offset = 0;
        while (offset < segment.Length)
        {
            if (!TryReadEbmlElement(segment, ref offset, out var elementId, out var tracks)) break;
            if (elementId != tracksElementId) continue;

            var tracksOffset = 0;
            while (tracksOffset < tracks.Length)
            {
                if (!TryReadEbmlElement(tracks, ref tracksOffset, out var trackElementId, out var trackEntry)) break;
                if (trackElementId != trackEntryElementId) continue;

                uint trackNumber = 0;
                ulong trackType = 0;
                ulong? defaultDuration = null;
                var entryOffset = 0;
                while (entryOffset < trackEntry.Length)
                {
                    if (!TryReadEbmlElement(trackEntry, ref entryOffset, out var fieldId, out var field)) break;
                    if (fieldId == trackNumberElementId && TryReadWebmUnsigned(field, out var number) && number <= uint.MaxValue)
                    {
                        trackNumber = (uint)number;
                    }
                    else if (fieldId == trackTypeElementId)
                    {
                        TryReadWebmUnsigned(field, out trackType);
                    }
                    else if (fieldId == defaultDurationElementId && TryReadWebmUnsigned(field, out var duration))
                    {
                        defaultDuration = duration;
                    }
                }

                if (trackNumber != 0 && trackType == 1 && defaultDuration is > 0)
                {
                    durations[trackNumber] = defaultDuration.Value;
                }
                else if (trackNumber != 0 && trackType == 1)
                {
                    durations[trackNumber] = 0;
                }
            }
        }

        return durations;
    }

    private static bool TryReadWebmBlock(
        ReadOnlySpan<byte> block,
        out uint trackNumber,
        out short relativeTimecode,
        out int frameCount)
    {
        trackNumber = 0;
        relativeTimecode = 0;
        frameCount = 0;
        if (block.Length == 0) return false;

        var trackNumberLength = EbmlLength(block[0]);
        if (trackNumberLength == 0 || trackNumberLength > 4 || block.Length < trackNumberLength + 3) return false;

        var trackNumberValue = (uint)(block[0] & (byte)((1 << (8 - trackNumberLength)) - 1));
        for (var index = 1; index < trackNumberLength; index++)
        {
            trackNumberValue = (trackNumberValue << 8) | block[index];
        }

        if (trackNumberValue == 0) return false;
        trackNumber = trackNumberValue;
        relativeTimecode = (short)((block[trackNumberLength] << 8) | block[trackNumberLength + 1]);
        var flagsOffset = trackNumberLength + 2;
        var lacing = block[flagsOffset] & 0x06;
        frameCount = lacing == 0 ? 1 : block.Length > flagsOffset + 1 ? block[flagsOffset + 1] + 1 : 0;
        return frameCount > 0;
    }

    private static bool TryGetWebmBlockDuration(
        uint trackNumber,
        int frameCount,
        ulong? explicitDurationTicks,
        IReadOnlyDictionary<uint, ulong> videoTrackDurations,
        long timecodeScale,
        out double durationTicks)
    {
        durationTicks = 0;
        if (explicitDurationTicks is > 0)
        {
            durationTicks = explicitDurationTicks.Value;
            return true;
        }

        if (!videoTrackDurations.TryGetValue(trackNumber, out var defaultDurationNs)
            || defaultDurationNs == 0
            || timecodeScale <= 0)
        {
            return false;
        }

        durationTicks = (double)defaultDurationNs * frameCount / timecodeScale;
        return durationTicks > 0;
    }

    private static bool TryReadWebmUnsigned(ReadOnlySpan<byte> value, out ulong result)
    {
        result = 0;
        if (value.Length is <= 0 or > 8) return false;
        foreach (var part in value) result = (result << 8) | part;
        return true;
    }

    private static bool TryReadEbmlElement(ReadOnlySpan<byte> content, ref int offset, out uint elementId, out ReadOnlySpan<byte> elementContent)
    {
        elementId = 0;
        elementContent = default;
        if (offset >= content.Length) return false;

        var elementIdLength = EbmlLength(content[offset]);
        if (elementIdLength == 0 || elementIdLength > 4 || offset + elementIdLength >= content.Length) return false;
        for (var index = 0; index < elementIdLength; index++) elementId = (elementId << 8) | content[offset + index];
        offset += elementIdLength;

        var sizeLength = EbmlLength(content[offset]);
        if (sizeLength == 0 || offset + sizeLength > content.Length) return false;
        ulong size = (uint)(content[offset] & (byte)((1 << (8 - sizeLength)) - 1));
        for (var index = 1; index < sizeLength; index++) size = (size << 8) | content[offset + index];
        offset += sizeLength;
        if (size == (1UL << (sizeLength * 7)) - 1)
        {
            var contentStart = offset;
            if (elementId == 0x1F43B675)
            {
                var probe = contentStart;
                while (probe < content.Length)
                {
                    if (!TryReadEbmlElementId(content, probe, out var siblingId, out _)) break;
                    if (IsWebmSegmentLevelElement(siblingId))
                    {
                        elementContent = content[contentStart..probe];
                        offset = probe;
                        return true;
                    }

                    var childOffset = probe;
                    if (!TryReadEbmlElement(content, ref childOffset, out _, out _) || childOffset <= probe) break;
                    probe = childOffset;
                }
            }

            elementContent = content[contentStart..];
            offset = content.Length;
            return true;
        }
        if (size > (ulong)(content.Length - offset)) return false;

        elementContent = content.Slice(offset, (int)size);
        offset += (int)size;
        return true;
    }

    private static bool TryReadEbmlElementId(ReadOnlySpan<byte> content, int offset, out uint elementId, out int elementIdLength)
    {
        elementId = 0;
        elementIdLength = 0;
        if (offset >= content.Length) return false;

        elementIdLength = EbmlLength(content[offset]);
        if (elementIdLength == 0 || elementIdLength > 4 || offset + elementIdLength >= content.Length) return false;
        for (var index = 0; index < elementIdLength; index++)
        {
            elementId = (elementId << 8) | content[offset + index];
        }

        return true;
    }

    private static bool IsWebmSegmentLevelElement(uint elementId) =>
        elementId is 0x114D9B74 or 0x1549A966 or 0x1654AE6B or 0x1F43B675
            or 0x1C53BB6B or 0x1941A469 or 0x1043A770 or 0x1254C367;

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
