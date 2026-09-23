using backend.Models;

namespace backend.Services;

public static class CaseMediaGate
{
    public static bool BlocksPublication(MediaStatus status) =>
        status is MediaStatus.Pending or MediaStatus.Uploading or MediaStatus.Processing or MediaStatus.Rejected or MediaStatus.Failed;

    public static MediaStatus ResolveStatus(string? mediaUrl) =>
        string.IsNullOrWhiteSpace(mediaUrl) ? MediaStatus.None : MediaStatus.Ready;

    /// A case remains hidden from the public feed until both sides have ready media.
    /// Text-only and partially completed cases stay out of the public queue.
    public static bool IsFeedReady(ArgumentCase argumentCase) =>
        argumentCase.SideA.MediaStatus == MediaStatus.Ready
        && argumentCase.SideB is not null
        && argumentCase.SideB.MediaStatus == MediaStatus.Ready;
}
