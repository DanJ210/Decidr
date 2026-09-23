using System.Collections.Concurrent;
using backend.Controllers;
using backend.Services;

namespace backend.Tests;

internal sealed class TestCaseMediaUploadSessionStore : ICaseMediaUploadSessionStore
{
    private readonly ConcurrentDictionary<Guid, CasesController.CaseMediaUploadSession> _sessions = new();

    public Task<CasesController.CaseMediaUploadSession?> GetAsync(Guid uploadId, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.TryGetValue(uploadId, out var session) ? session : null);

    public Task SaveAsync(CasesController.CaseMediaUploadSession session, CancellationToken cancellationToken)
    {
        _sessions[session.UploadId] = session;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<CasesController.CaseMediaUploadSession>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<CasesController.CaseMediaUploadSession>>(_sessions.Values.ToList());

    public Task<bool> DeleteAsync(Guid uploadId, CancellationToken cancellationToken) =>
        Task.FromResult(_sessions.TryRemove(uploadId, out _));
}
