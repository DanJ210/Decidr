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
}
