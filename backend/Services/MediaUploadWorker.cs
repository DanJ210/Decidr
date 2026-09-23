using backend.Controllers;

namespace backend.Services;

public sealed class MediaUploadWorker(
    MediaUploadProcessingQueue queue,
    ICaseEvidenceStorage storage,
    ICaseMediaUploadSessionStore sessionStore,
    ILogger<MediaUploadWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var uploadId in queue.ReadAllAsync(stoppingToken))
        {
            try
            {
                await CasesController.ProcessMediaUploadAsync(uploadId, storage, sessionStore, logger, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Media upload processing failed for {UploadId}.", uploadId);
            }
        }
    }
}
