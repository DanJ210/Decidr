using backend.Models;

namespace backend.Data.Entities;

public class CaseEvidenceEntity
{
    public Guid Id { get; set; }
    public Guid CaseId { get; set; }
    public CaseSide Side { get; set; }
    public Guid AddedByUserId { get; set; }
    public string AddedByUserName { get; set; } = "";
    public CaseEvidenceType Type { get; set; }
    public string Title { get; set; } = "";
    public string ResourceUrl { get; set; } = "";
    public string MimeType { get; set; } = "";
    public long SizeBytes { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
