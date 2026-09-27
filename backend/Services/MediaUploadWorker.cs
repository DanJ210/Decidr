using backend.Controllers;

namespace backend.Services;

public sealed class MediaUploadWorker(
    MediaUploadProcessingQueue queue,
    ICaseEvidenceStorage storage,
    ICaseMediaUploadSessionStore sessionStore,
    ILogger<MediaUploadWorker> logger) : BackgroundService
{
    private static readonly TimeSpan ScanPollInterval = TimeSpan.FromSeconds(5);
    private const int MaxScanPollAttempts = 60;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var uploadId in queue.ReadAllAsync(stoppingToken))
        {
            // Detached so one clip waiting on a malware-scan verdict cannot stall the queue.
            _ = ProcessAsync(uploadId, stoppingToken);
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
