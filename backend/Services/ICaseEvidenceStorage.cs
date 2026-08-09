namespace backend.Services;

public enum EvidenceContentStatus
{
    Clean,
    PendingScan,
    Malicious,
    ScanFailed,
    NotFound
}

public sealed record StoredEvidenceContent(
    EvidenceContentStatus Status,
    Stream? Content = null,
    string? ContentType = null);

public interface ICaseEvidenceStorage
{
    Task<string> UploadAsync(
        Guid caseId,
        string fileExtension,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);

    Task<StoredEvidenceContent> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken);
}