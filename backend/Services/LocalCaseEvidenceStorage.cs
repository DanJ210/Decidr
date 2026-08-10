namespace backend.Services;

public sealed class LocalCaseEvidenceStorage(
    IWebHostEnvironment environment) : ICaseEvidenceStorage
{
    private readonly string _rootPath = Path.Combine(
        environment.ContentRootPath,
        "App_Data",
        "case-evidence");

    public async Task<string> UploadAsync(
        Guid caseId,
        string fileExtension,
        string contentType,
        Stream content,
        CancellationToken cancellationToken)
    {
        var storageKey = $"{caseId:N}/{Guid.NewGuid():N}{fileExtension}";
        var fullPath = GetFullPath(storageKey);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

        await using var destination = File.Create(fullPath);
        await content.CopyToAsync(destination, cancellationToken);
        return storageKey;
    }

    public Task<StoredEvidenceContent> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath(storageKey);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult(new StoredEvidenceContent(EvidenceContentStatus.NotFound));
        }

        var content = File.OpenRead(fullPath);
        var contentType = Path.GetExtension(fullPath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webp" => "image/webp",
            ".gif" => "image/gif",
            ".pdf" => "application/pdf",
            ".txt" => "text/plain",
            ".doc" => "application/msword",
            ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            _ => "application/octet-stream",
        };

        return Task.FromResult(new StoredEvidenceContent(
            EvidenceContentStatus.Clean,
            content,
            contentType));
    }

    public Task<EvidenceContentStatus> GetStatusAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(
            File.Exists(GetFullPath(storageKey))
                ? EvidenceContentStatus.Clean
                : EvidenceContentStatus.NotFound);
    }

    public Task DeleteAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath(storageKey);
        if (File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return Task.CompletedTask;
    }

    private string GetFullPath(string storageKey)
    {
        var normalizedKey = storageKey.Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, normalizedKey));
        var rootPath = Path.GetFullPath(_rootPath) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid evidence storage key.");
        }

        return fullPath;
    }
}