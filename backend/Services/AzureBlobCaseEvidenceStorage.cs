using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using backend.Models;

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
        CancellationToken cancellationToken,
        string? storageKey = null)
    {
        storageKey ??= $"{caseId:N}/{Guid.NewGuid():N}{fileExtension}";
        var blobClient = containerClient.GetBlobClient(storageKey);

        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

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
            var scanStatus = await GetStatusAsync(storageKey, cancellationToken);
            if (scanStatus != EvidenceContentStatus.Clean)
            {
                logger.LogWarning(
                    "Blocked evidence blob {StorageKey} with malware scan status {ScanStatus}.",
                    storageKey,
                    scanStatus);
                return new StoredEvidenceContent(scanStatus);
            }

            var properties = await blobClient.GetPropertiesAsync(cancellationToken: cancellationToken);
            // A seekable stream lets ASP.NET serve HTTP Range (206) responses, which iOS Safari requires for video.
            var content = await blobClient.OpenReadAsync(
                new BlobOpenReadOptions(allowModifications: false)
                {
                    Conditions = new BlobRequestConditions { IfMatch = properties.Value.ETag },
                },
                cancellationToken);

            return new StoredEvidenceContent(
                EvidenceContentStatus.Clean,
                content,
                properties.Value.ContentType ?? "application/octet-stream");
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

    public async Task<EvidenceContentStatus> GetStatusAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var tagsResponse = await containerClient
                .GetBlobClient(storageKey)
                .GetTagsAsync(cancellationToken: cancellationToken);
            return GetEvidenceContentStatus(tagsResponse.Value.Tags);
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return EvidenceContentStatus.NotFound;
        }
        catch (RequestFailedException exception) when (exception.Status is StatusCodes.Status401Unauthorized or StatusCodes.Status403Forbidden)
        {
            logger.LogError(
                exception,
                "Case evidence blob {StorageKey} could not be security-checked because storage access was denied.",
                storageKey);
            return EvidenceContentStatus.ScanFailed;
        }
        catch (RequestFailedException exception)
        {
            logger.LogError(
                exception,
                "Case evidence blob {StorageKey} could not be security-checked because storage failed with status {StorageStatus} and error code {StorageErrorCode}.",
                storageKey,
                exception.Status,
                exception.ErrorCode);
            return EvidenceContentStatus.ScanFailed;
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