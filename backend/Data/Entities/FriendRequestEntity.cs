using backend.Models;

namespace backend.Data.Entities;

public class FriendRequestEntity
{
    public Guid Id { get; set; }
    public Guid FromUserId { get; set; }
    public Guid ToUserId { get; set; }
    public FriendRequestStatus Status { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
