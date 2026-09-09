using backend.Models;

namespace backend.Services;

public static class CaseMediaGate
{
    public static bool BlocksPublication(MediaStatus status) =>
        status is MediaStatus.Pending or MediaStatus.Failed;

    public static MediaStatus ResolveStatus(string? mediaUrl) =>
        string.IsNullOrWhiteSpace(mediaUrl) ? MediaStatus.None : MediaStatus.Ready;

    /// A case stays out of the public feed until every side that declares media is ready.
    public static bool IsFeedReady(ArgumentCase argumentCase) =>
        !BlocksPublication(argumentCase.SideA.MediaStatus)
        && (argumentCase.SideB is null || !BlocksPublication(argumentCase.SideB.MediaStatus));
}
