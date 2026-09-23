using System.Text.Json;
using System.Text.Json.Serialization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using backend.Controllers;

namespace backend.Services;

public interface ICaseMediaUploadSessionStore
{
    Task<CasesController.CaseMediaUploadSession?> GetAsync(Guid uploadId, CancellationToken cancellationToken);
    Task SaveAsync(CasesController.CaseMediaUploadSession session, CancellationToken cancellationToken);
    Task<IReadOnlyList<CasesController.CaseMediaUploadSession>> ListAsync(CancellationToken cancellationToken);
    Task<bool> DeleteAsync(Guid uploadId, CancellationToken cancellationToken);
}

public sealed class LocalCaseMediaUploadSessionStore(
    IWebHostEnvironment environment) : ICaseMediaUploadSessionStore
{
    private static readonly JsonSerializerOptions JsonOptions = CaseMediaUploadSessionStoreJson.Options;
    private readonly string _rootPath = Path.Combine(
        environment.ContentRootPath,
        "App_Data",
        "case-media-upload-sessions");

    public async Task<CasesController.CaseMediaUploadSession?> GetAsync(Guid uploadId, CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath(uploadId);
        if (!File.Exists(fullPath))
        {
            return null;
        }

        await using var content = File.OpenRead(fullPath);
        return await JsonSerializer.DeserializeAsync<CasesController.CaseMediaUploadSession>(
            content,
            JsonOptions,
            cancellationToken);
    }

    public async Task SaveAsync(CasesController.CaseMediaUploadSession session, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(_rootPath);
        var fullPath = GetFullPath(session.UploadId);
        var tempPath = $"{fullPath}.{Guid.NewGuid():N}.tmp";
        var json = JsonSerializer.Serialize(session, JsonOptions);
        await File.WriteAllTextAsync(tempPath, json, cancellationToken);
        File.Move(tempPath, fullPath, true);
    }

    public async Task<IReadOnlyList<CasesController.CaseMediaUploadSession>> ListAsync(CancellationToken cancellationToken)
    {
        if (!Directory.Exists(_rootPath))
        {
            return [];
        }

        var sessions = new List<CasesController.CaseMediaUploadSession>();
        foreach (var path in Directory.EnumerateFiles(_rootPath, "*.json", SearchOption.TopDirectoryOnly))
        {
            await using var content = File.OpenRead(path);
            var session = await JsonSerializer.DeserializeAsync<CasesController.CaseMediaUploadSession>(
                content,
                JsonOptions,
                cancellationToken);
            if (session is not null)
            {
                sessions.Add(session);
            }
        }

        return sessions;
    }

    public Task<bool> DeleteAsync(Guid uploadId, CancellationToken cancellationToken)
    {
        var fullPath = GetFullPath(uploadId);
        if (!File.Exists(fullPath))
        {
            return Task.FromResult(false);
        }

        File.Delete(fullPath);
        return Task.FromResult(true);
    }

    private string GetFullPath(Guid uploadId)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_rootPath, $"{uploadId:N}.json"));
        var rootPath = Path.GetFullPath(_rootPath) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid upload session path.");
        }

        return fullPath;
    }
}

public sealed class BlobCaseMediaUploadSessionStore(
    BlobContainerClient containerClient) : ICaseMediaUploadSessionStore
{
    private const string Prefix = "media-upload-sessions";
    private static readonly JsonSerializerOptions JsonOptions = CaseMediaUploadSessionStoreJson.Options;

    public async Task<CasesController.CaseMediaUploadSession?> GetAsync(Guid uploadId, CancellationToken cancellationToken)
    {
        try
        {
            var response = await GetBlobClient(uploadId)
                .DownloadStreamingAsync(cancellationToken: cancellationToken);
            return await JsonSerializer.DeserializeAsync<CasesController.CaseMediaUploadSession>(
                response.Value.Content,
                JsonOptions,
                cancellationToken);
        }
        catch (RequestFailedException exception) when (exception.Status == StatusCodes.Status404NotFound)
        {
            return null;
        }
    }

    public async Task SaveAsync(CasesController.CaseMediaUploadSession session, CancellationToken cancellationToken)
    {
        var blobClient = GetBlobClient(session.UploadId);
        await blobClient.DeleteIfExistsAsync(cancellationToken: cancellationToken);

        await using var content = new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(session, JsonOptions));
        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = "application/json",
                },
            },
            cancellationToken);
    }

    public async Task<IReadOnlyList<CasesController.CaseMediaUploadSession>> ListAsync(CancellationToken cancellationToken)
    {
        var sessions = new List<CasesController.CaseMediaUploadSession>();
        await foreach (var blobItem in containerClient.GetBlobsAsync(
            traits: BlobTraits.None,
            states: BlobStates.None,
            prefix: Prefix + "/",
            cancellationToken: cancellationToken))
        {
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(blobItem.Name), "N", out var uploadId))
            {
                continue;
            }

            var session = await GetAsync(uploadId, cancellationToken);
            if (session is not null)
            {
                sessions.Add(session);
            }
        }

        return sessions;
    }

    public async Task<bool> DeleteAsync(Guid uploadId, CancellationToken cancellationToken)
    {
        var response = await GetBlobClient(uploadId).DeleteIfExistsAsync(cancellationToken: cancellationToken);
        return response.Value;
    }

    private BlobClient GetBlobClient(Guid uploadId) =>
        containerClient.GetBlobClient($"{Prefix}/{uploadId:N}.json");
}

internal static class CaseMediaUploadSessionStoreJson
{
    internal static readonly JsonSerializerOptions Options = Create();

    private static JsonSerializerOptions Create()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
