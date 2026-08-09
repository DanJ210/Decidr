using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace backend.Services;

public sealed class AzureBlobCaseEvidenceStorage(
    BlobContainerClient containerClient,
    ILogger<AzureBlobCaseEvidenceStorage> logger) : ICaseEvidenceStorage
{
    internal const string MalwareScanResultTag = "Malware Scanning scan result";

    public async Task<string> UploadAsync(
        Guid caseId,
        string fileExtension,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        var storageKey = $"{caseId:N}/{Guid.NewGuid():N}{fileExtension}";
        var blobClient = containerClient.GetBlobClient(storageKey);

        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType,
                },
            },
            cancellationToken);

        logger.LogInformation("Uploaded case evidence blob {StorageKey}.", storageKey);
        return storageKey;
    }

    public async Task<StoredEvidenceContent> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var blobClient = containerClient.GetBlobClient(storageKey);
            var tagsResponse = await blobClient.GetTagsAsync(cancellationToken: cancellationToken);
            var scanStatus = GetEvidenceContentStatus(tagsResponse.Value.Tags);
            if (scanStatus != EvidenceContentStatus.Clean)
            {
                logger.LogWarning(
                    "Blocked evidence blob {StorageKey} with malware scan status {ScanStatus}.",
                    storageKey,
                    scanStatus);
                return new StoredEvidenceContent(scanStatus);
            }

            var response = await blobClient
                .DownloadStreamingAsync(cancellationToken: cancellationToken);

            return new StoredEvidenceContent(
                EvidenceContentStatus.Clean,
                response.Value.Content,
                response.Value.Details.ContentType ?? "application/octet-stream");
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            logger.LogWarning("Case evidence blob {StorageKey} was not found.", storageKey);
            return new StoredEvidenceContent(EvidenceContentStatus.NotFound);
        }
        catch (RequestFailedException exception) when (exception.Status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
        {
            logger.LogError(
                exception,
                "Case evidence blob {StorageKey} could not be security-checked because storage access was denied.",
                storageKey);
            return new StoredEvidenceContent(EvidenceContentStatus.ScanFailed);
        }
    }

    public async Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        await containerClient
            .GetBlobClient(storageKey)
            .DeleteIfExistsAsync(cancellationToken: cancellationToken);
    }

    internal static EvidenceContentStatus GetEvidenceContentStatus(
        IDictionary<string, string> tags)
    {
        if (!tags.TryGetValue(MalwareScanResultTag, out var scanResult))
        {
            return EvidenceContentStatus.PendingScan;
        }

        return scanResult switch
        {
            "No threats found" => EvidenceContentStatus.Clean,
            "Malicious" => EvidenceContentStatus.Malicious,
            "Error" or "Not scanned" or "Not Scanned" => EvidenceContentStatus.ScanFailed,
            _ => EvidenceContentStatus.PendingScan,
        };
    }
}