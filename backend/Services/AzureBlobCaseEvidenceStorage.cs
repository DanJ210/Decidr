using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace backend.Services;

public sealed class AzureBlobCaseEvidenceStorage(
    BlobContainerClient containerClient,
    ILogger<AzureBlobCaseEvidenceStorage> logger) : ICaseEvidenceStorage
{
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

    public async Task<StoredEvidenceContent?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await containerClient
                .GetBlobClient(storageKey)
                .DownloadStreamingAsync(cancellationToken: cancellationToken);

            return new StoredEvidenceContent(
                response.Value.Content,
                response.Value.Details.ContentType ?? "application/octet-stream");
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            logger.LogWarning("Case evidence blob {StorageKey} was not found.", storageKey);
            return null;
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
}