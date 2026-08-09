using System.IO.Compression;
using System.Text;

namespace backend.Services;

public static class EvidenceFileValidator
{
    private static readonly byte[] JpegSignature = [0xFF, 0xD8, 0xFF];
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Gif87Signature = "GIF87a"u8.ToArray();
    private static readonly byte[] Gif89Signature = "GIF89a"u8.ToArray();
    private static readonly byte[] PdfSignature = "%PDF-"u8.ToArray();
    private static readonly byte[] CompoundDocumentSignature = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];

    public static async Task<bool> IsValidAsync(
        IFormFile file,
        string extension,
        CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        return extension.ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => await StartsWithAsync(stream, JpegSignature, cancellationToken),
            ".png" => await StartsWithAsync(stream, PngSignature, cancellationToken),
            ".gif" => await StartsWithAnyAsync(stream, [Gif87Signature, Gif89Signature], cancellationToken),
            ".webp" => await IsWebPAsync(stream, cancellationToken),
            ".pdf" => await StartsWithAsync(stream, PdfSignature, cancellationToken),
            ".doc" => await StartsWithAsync(stream, CompoundDocumentSignature, cancellationToken),
            ".docx" => IsDocx(stream),
            ".txt" => await IsUtf8TextAsync(stream, cancellationToken),
            _ => false,
        };
    }

    private static async Task<bool> StartsWithAsync(
        Stream stream,
        byte[] signature,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[signature.Length];
        return await stream.ReadAtLeastAsync(buffer, signature.Length, throwOnEndOfStream: false, cancellationToken) == signature.Length
            && buffer.AsSpan().SequenceEqual(signature);
    }

    private static async Task<bool> StartsWithAnyAsync(
        Stream stream,
        IReadOnlyList<byte[]> signatures,
        CancellationToken cancellationToken)
    {
        var signatureLength = signatures.Max(signature => signature.Length);
        var buffer = new byte[signatureLength];
        var bytesRead = await stream.ReadAtLeastAsync(buffer, signatureLength, throwOnEndOfStream: false, cancellationToken);
        return signatures.Any(signature =>
            bytesRead >= signature.Length && buffer.AsSpan(0, signature.Length).SequenceEqual(signature));
    }

    private static async Task<bool> IsWebPAsync(Stream stream, CancellationToken cancellationToken)
    {
        var header = new byte[12];
        if (await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, cancellationToken) != header.Length)
        {
            return false;
        }

        return header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
            && header.AsSpan(8, 4).SequenceEqual("WEBP"u8);
    }

    private static bool IsDocx(Stream stream)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            return archive.GetEntry("[Content_Types].xml") is not null
                && archive.GetEntry("word/document.xml") is not null;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    private static async Task<bool> IsUtf8TextAsync(Stream stream, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = new StreamReader(
                stream,
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
                detectEncodingFromByteOrderMarks: true,
                leaveOpen: true);
            var buffer = new char[4096];
            while (await reader.ReadAsync(buffer, cancellationToken) > 0)
            {
                if (buffer.Contains('\0'))
                {
                    return false;
                }
            }

            return true;
        }
        catch (DecoderFallbackException)
        {
            return false;
        }
    }
}