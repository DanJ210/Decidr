using backend.Models;

namespace backend.Data.Entities;

public class CaseVoteEntity
{
    public Guid CaseId { get; set; }
    public Guid UserId { get; set; }
    public CaseSide Side { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public int ChangeCount { get; set; }
}
