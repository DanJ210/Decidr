using System.Threading.Channels;

namespace backend.Services;

public sealed class MediaUploadProcessingQueue
{
    private readonly Channel<Guid> _queue = Channel.CreateUnbounded<Guid>(
        new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });

    public ValueTask EnqueueAsync(Guid uploadId, CancellationToken cancellationToken) =>
        _queue.Writer.WriteAsync(uploadId, cancellationToken);

    public IAsyncEnumerable<Guid> ReadAllAsync(CancellationToken cancellationToken) =>
        _queue.Reader.ReadAllAsync(cancellationToken);
}
