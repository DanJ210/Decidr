using System.Buffers.Binary;
using backend.Services;
using Xunit;

namespace backend.Tests;

public sealed class VideoFileValidatorTests
{
    [Fact]
    public async Task Webm_duration_is_measured_from_clusters_when_the_segment_declares_none()
    {
        using var content = new MemoryStream(LiveWebm(clusterTimecodeMs: 2000, blockOffsetMs: 900));

        var duration = await VideoFileValidator.GetDurationSecondsAsync(content, ".webm", CancellationToken.None);

        Assert.Equal(3, duration);
    }

    [Fact]
    public async Task Fragmented_mp4_duration_is_measured_from_fragments_when_mvhd_is_zero()
    {
        using var content = new MemoryStream(FragmentedMp4(baseDecodeTicks: 0, sampleCount: 30, sampleDurationTicks: 100));

        var duration = await VideoFileValidator.GetDurationSecondsAsync(content, ".mp4", CancellationToken.None);

        Assert.Equal(3, duration);
    }

    [Fact]
    public async Task Fragmented_mp4_duration_is_measured_from_the_first_decode_time()
    {
        // Safari's MediaRecorder starts tfdt at a capture-clock offset rather than zero.
        using var content = new MemoryStream(FragmentedMp4(
            baseDecodeTicks: 3_600_000,
            sampleCount: 6,
            sampleDurationTicks: 1_000,
            fragmentCount: 2));

        var duration = await VideoFileValidator.GetDurationSecondsAsync(content, ".mp4", CancellationToken.None);

        Assert.Equal(12, duration);
    }

    [Fact]
    public async Task Fragmented_mp4_duration_beyond_the_clip_limit_is_reported_in_full()
    {
        using var content = new MemoryStream(FragmentedMp4(baseDecodeTicks: 90_000, sampleCount: 100, sampleDurationTicks: 1_000));

        var duration = await VideoFileValidator.GetDurationSecondsAsync(content, ".mp4", CancellationToken.None);

        Assert.Equal(100, duration);
    }

    [Fact]
    public async Task Fragmented_mp4_without_tfdt_continues_decode_time_between_fragments()
    {
        using var content = new MemoryStream(FragmentedMp4WithoutTfdt(fragmentCount: 6, fragmentDurationTicks: 10_000));

        var duration = await VideoFileValidator.GetDurationSecondsAsync(content, ".mp4", CancellationToken.None);

        Assert.Equal(60, duration);
    }

    [Fact]
    public async Task Fragmented_mp4_uses_trex_default_sample_duration()
    {
        using var content = new MemoryStream(FragmentedMp4WithTrexDefault(
            baseDecodeTicks: 29_000,
            sampleCount: 31,
            defaultSampleDurationTicks: 1_000));

        var duration = await VideoFileValidator.GetDurationSecondsAsync(content, ".mp4", CancellationToken.None);

        Assert.Equal(31, duration);
    }

    [Fact]
    public async Task Fragmented_mp4_rejects_truncated_version_one_tfdt()
    {
        using var content = new MemoryStream(FragmentedMp4WithMalformedTfdt());

        var duration = await VideoFileValidator.GetDurationSecondsAsync(content, ".mp4", CancellationToken.None);

        Assert.Null(duration);
    }

    [Fact]
    public async Task Webm_unknown_sized_clusters_are_measured_independently()
    {
        using var content = new MemoryStream(LiveWebm(
            Cluster(timecodeMs: 2_000, blockOffsetMs: 900, unknownSize: true),
            Cluster(timecodeMs: 40_000, blockOffsetMs: 900, unknownSize: true)));

        var duration = await VideoFileValidator.GetDurationSecondsAsync(content, ".webm", CancellationToken.None);

        Assert.Equal(41, duration);
    }

    [Fact]
    public async Task Webm_duration_includes_the_final_block_duration()
    {
        using var content = new MemoryStream(LiveWebm(
            BlockGroupCluster(timecodeMs: 29_000, blockOffsetMs: 0, blockDurationMs: 2_000)));

        var duration = await VideoFileValidator.GetDurationSecondsAsync(content, ".webm", CancellationToken.None);

        Assert.Equal(31, duration);
    }

    private static byte[] LiveWebm(int clusterTimecodeMs, short blockOffsetMs)
    {
        return LiveWebm(Cluster(clusterTimecodeMs, blockOffsetMs));
    }

    private static byte[] LiveWebm(params byte[][] clusters)
    {
        var defaultDuration = UInt32(100_000_000);
        var trackEntry = Ebml(
            EbmlElement([0xD7], [0x01]),
            EbmlElement([0x83], [0x01]),
            EbmlElement([0x23, 0xE3, 0x83], defaultDuration));
        var tracks = EbmlElement([0x16, 0x54, 0xAE, 0x6B], EbmlElement([0xAE], trackEntry));
        var info = EbmlElement(
            [0x15, 0x49, 0xA9, 0x66],
            EbmlElement([0x2A, 0xD7, 0xB1], [0x0F, 0x42, 0x40]));
        return
        [
            0x1A, 0x45, 0xDF, 0xA3, 0x80,
            0x18, 0x53, 0x80, 0x67, 0xFF,
            .. info,
            .. tracks,
            .. clusters.SelectMany(cluster => cluster),
        ];
    }

    private static byte[] Cluster(int timecodeMs, short blockOffsetMs, bool unknownSize = false)
    {
        // Track number vint, signed relative timecode, flags, then a single payload byte.
        byte[] simpleBlock = [0x81, (byte)(blockOffsetMs >> 8), (byte)blockOffsetMs, 0x00, 0x00];
        byte[] clusterContent =
        [
            0xE7, 0x82, (byte)(timecodeMs >> 8), (byte)timecodeMs,
            0xA3, (byte)(0x80 | simpleBlock.Length), .. simpleBlock,
        ];
        return unknownSize
            ? [0x1F, 0x43, 0xB6, 0x75, 0xFF, .. clusterContent]
            : EbmlElement([0x1F, 0x43, 0xB6, 0x75], clusterContent);
    }

    private static byte[] BlockGroupCluster(int timecodeMs, short blockOffsetMs, int blockDurationMs)
    {
        byte[] block = [0x81, (byte)(blockOffsetMs >> 8), (byte)blockOffsetMs, 0x00, 0x00];
        var blockGroup = EbmlElement(
            [0xA0],
            Ebml(
                EbmlElement([0xA1], block),
                EbmlElement([0x9B], UInt32((uint)blockDurationMs))));
        var clusterContent = Ebml(
            EbmlElement([0xE7], [(byte)(timecodeMs >> 8), (byte)timecodeMs]),
            blockGroup);
        return EbmlElement([0x1F, 0x43, 0xB6, 0x75], clusterContent);
    }

    private static byte[] FragmentedMp4(uint baseDecodeTicks, uint sampleCount, uint sampleDurationTicks, int fragmentCount = 1)
    {
        const uint trackId = 1;

        var moov = MovieBox();

        // tf_flags 0x08 marks a default sample duration; tr_flags 0 makes every sample use it.
        var fragments = Enumerable.Range(0, fragmentCount)
            .SelectMany(index =>
            {
                var tfhd = Box("tfhd", [.. UInt32(0x08), .. UInt32(trackId), .. UInt32(sampleDurationTicks)]);
                var tfdt = Box("tfdt", [.. UInt32(0), .. UInt32(baseDecodeTicks + (uint)index * sampleCount * sampleDurationTicks)]);
                var trun = Box("trun", [.. UInt32(0), .. UInt32(sampleCount)]);
                return Box("moof", Box("traf", [.. tfhd, .. tfdt, .. trun]));
            });

        return [.. Box("ftyp", "isom\0\0\0\0"u8.ToArray()), .. moov, .. fragments];
    }

    private static byte[] FragmentedMp4WithoutTfdt(int fragmentCount, uint fragmentDurationTicks)
    {
        const uint trackId = 1;
        var fragments = Enumerable.Range(0, fragmentCount)
            .Select(_ =>
            {
                var tfhd = Box("tfhd", [.. UInt32(0), .. UInt32(trackId)]);
                var trun = Box("trun", [.. UInt32(0x100), .. UInt32(1), .. UInt32(fragmentDurationTicks)]);
                return Box("moof", Box("traf", [.. tfhd, .. trun]));
            })
            .SelectMany(fragment => fragment);
        return [.. Box("ftyp", "isom\0\0\0\0"u8.ToArray()), .. MovieBox(), .. fragments];
    }

    private static byte[] FragmentedMp4WithTrexDefault(uint baseDecodeTicks, uint sampleCount, uint defaultSampleDurationTicks)
    {
        const uint trackId = 1;
        var tfhd = Box("tfhd", [.. UInt32(0), .. UInt32(trackId)]);
        var tfdt = Box("tfdt", [.. UInt32(0), .. UInt32(baseDecodeTicks)]);
        var trun = Box("trun", [.. UInt32(0), .. UInt32(sampleCount)]);
        var moof = Box("moof", Box("traf", [.. tfhd, .. tfdt, .. trun]));
        return
        [
            .. Box("ftyp", "isom\0\0\0\0"u8.ToArray()),
            .. MovieBox(defaultSampleDurationTicks),
            .. moof,
        ];
    }

    private static byte[] FragmentedMp4WithMalformedTfdt()
    {
        const uint trackId = 1;
        var tfhd = Box("tfhd", [.. UInt32(0), .. UInt32(trackId)]);
        var tfdt = Box("tfdt", [1, 0, 0, 0, 0, 0, 0, 0]);
        var trun = Box("trun", [.. UInt32(0), .. UInt32(2)]);
        var moof = Box("moof", Box("traf", [.. tfhd, .. tfdt, .. trun]));
        return
        [
            .. Box("ftyp", "isom\0\0\0\0"u8.ToArray()),
            .. MovieBox(defaultSampleDurationTicks: 1_000),
            .. moof,
        ];
    }

    private static byte[] MovieBox(uint defaultSampleDurationTicks = 0)
    {
        const uint timeScale = 1_000;
        const uint trackId = 1;
        var mvhd = Box("mvhd", [.. new byte[12], .. UInt32(timeScale), .. UInt32(0)]);
        var tkhd = Box("tkhd", [.. new byte[12], .. UInt32(trackId), .. new byte[4]]);
        var mdhd = Box("mdhd", [.. new byte[12], .. UInt32(timeScale), .. UInt32(0)]);
        var trak = Box("trak", [.. tkhd, .. Box("mdia", mdhd)]);
        var trex = Box("trex", [.. UInt32(0), .. UInt32(trackId), .. UInt32(1), .. UInt32(defaultSampleDurationTicks), .. UInt32(0), .. UInt32(0)]);
        return Box("moov", [.. mvhd, .. trak, .. Box("mvex", trex)]);
    }

    private static byte[] EbmlElement(byte[] id, byte[] content) =>
        [.. id, (byte)(0x80 | content.Length), .. content];

    private static byte[] Ebml(params byte[][] elements) =>
        [.. elements.SelectMany(element => element)];

    private static byte[] Box(string type, byte[] content) =>
        [.. UInt32((uint)(content.Length + 8)), .. System.Text.Encoding.ASCII.GetBytes(type), .. content];

    private static byte[] UInt32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }
}
