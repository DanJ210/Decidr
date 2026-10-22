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
        using var content = new MemoryStream(FragmentedMp4(baseDecodeTicks: 2800, sampleCount: 2, sampleDurationTicks: 100));

        var duration = await VideoFileValidator.GetDurationSecondsAsync(content, ".mp4", CancellationToken.None);

        Assert.Equal(3, duration);
    }

    [Fact]
    public async Task Fragmented_mp4_duration_beyond_the_clip_limit_is_reported_in_full()
    {
        using var content = new MemoryStream(FragmentedMp4(baseDecodeTicks: 90_000, sampleCount: 10, sampleDurationTicks: 1_000));

        var duration = await VideoFileValidator.GetDurationSecondsAsync(content, ".mp4", CancellationToken.None);

        Assert.Equal(100, duration);
    }

    private static byte[] LiveWebm(int clusterTimecodeMs, short blockOffsetMs)
    {
        // Track number vint, signed relative timecode, flags, then a single payload byte.
        byte[] simpleBlock = [0x81, (byte)(blockOffsetMs >> 8), (byte)blockOffsetMs, 0x00, 0x00];
        byte[] cluster =
        [
            0xE7, 0x82, (byte)(clusterTimecodeMs >> 8), (byte)clusterTimecodeMs,
            0xA3, (byte)(0x80 | simpleBlock.Length), .. simpleBlock,
        ];

        return
        [
            0x1A, 0x45, 0xDF, 0xA3, 0x80,
            0x18, 0x53, 0x80, 0x67, 0xFF,
            // Info carrying only a TimecodeScale of 1,000,000 ns.
            0x15, 0x49, 0xA9, 0x66, 0x87, 0x2A, 0xD7, 0xB1, 0x83, 0x0F, 0x42, 0x40,
            0x1F, 0x43, 0xB6, 0x75, (byte)(0x80 | cluster.Length), .. cluster,
        ];
    }

    private static byte[] FragmentedMp4(uint baseDecodeTicks, uint sampleCount, uint sampleDurationTicks)
    {
        const uint timeScale = 1_000;
        const uint trackId = 1;

        var mvhd = Box("mvhd", [.. new byte[12], .. UInt32(timeScale), .. UInt32(0)]);
        var tkhd = Box("tkhd", [.. new byte[12], .. UInt32(trackId), .. new byte[4]]);
        var mdhd = Box("mdhd", [.. new byte[12], .. UInt32(timeScale), .. UInt32(0)]);
        var moov = Box("moov", [.. mvhd, .. Box("trak", [.. tkhd, .. Box("mdia", mdhd)])]);

        // tf_flags 0x08 marks a default sample duration; tr_flags 0 makes every sample use it.
        var tfhd = Box("tfhd", [.. UInt32(0x08), .. UInt32(trackId), .. UInt32(sampleDurationTicks)]);
        var tfdt = Box("tfdt", [.. UInt32(0), .. UInt32(baseDecodeTicks)]);
        var trun = Box("trun", [.. UInt32(0), .. UInt32(sampleCount)]);
        var moof = Box("moof", Box("traf", [.. tfhd, .. tfdt, .. trun]));

        return [.. Box("ftyp", "isom\0\0\0\0"u8.ToArray()), .. moov, .. moof];
    }

    private static byte[] Box(string type, byte[] content) =>
        [.. UInt32((uint)(content.Length + 8)), .. System.Text.Encoding.ASCII.GetBytes(type), .. content];

    private static byte[] UInt32(uint value)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, value);
        return bytes;
    }
}
