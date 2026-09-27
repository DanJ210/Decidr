using backend.Controllers;

namespace backend.Services;

public sealed class MediaUploadWorker(
    MediaUploadProcessingQueue queue,
    ICaseEvidenceStorage storage,
    ICaseMediaUploadSessionStore sessionStore,
    ILogger<MediaUploadWorker> logger) : BackgroundService
{
    private const int MaxConcurrentUploads = 4;
    private static readonly TimeSpan ScanPollInterval = TimeSpan.FromSeconds(5);
    private const int MaxScanPollAttempts = 60;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workers = Enumerable
            .Range(0, MaxConcurrentUploads)
            .Select(_ => RunWorkerAsync(stoppingToken))
            .ToArray();
        await Task.WhenAll(workers);
    }

    private async Task RunWorkerAsync(CancellationToken stoppingToken)
    {
        await foreach (var uploadId in queue.ReadAllAsync(stoppingToken))
        {
            await ProcessAsync(uploadId, stoppingToken);
        }
    }

    private async Task ProcessAsync(Guid uploadId, CancellationToken stoppingToken)
    {
        try
        {
            for (var attempt = 0; attempt < MaxScanPollAttempts; attempt++)
            {
                if (await CasesController.ProcessMediaUploadAsync(uploadId, storage, sessionStore, logger, stoppingToken))
                {
                    return;
                }

                await Task.Delay(ScanPollInterval, stoppingToken);
            }

            if (await CasesController.ProcessMediaUploadAsync(uploadId, storage, sessionStore, logger, stoppingToken))
            {
                return;
            }

            logger.LogWarning("Media upload {UploadId} was never cleared by malware scanning.", uploadId);
            await CasesController.FailMediaUploadAsync(
                uploadId,
                "Video security scanning did not complete in time.",
                sessionStore,
                stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Media upload processing failed for {UploadId}.", uploadId);
        }
    }
}
