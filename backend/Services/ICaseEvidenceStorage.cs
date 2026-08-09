namespace backend.Services;

public sealed record StoredEvidenceContent(
    Stream Content,
    string ContentType);

public interface ICaseEvidenceStorage
{
    Task<string> UploadAsync(
        Guid caseId,
        string fileExtension,
        string contentType,
        Stream content,
        CancellationToken cancellationToken);

    Task<StoredEvidenceContent?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken);
}