using backend.Models;

namespace backend.Data.Entities;

public class UserEntity
{
    public Guid Id { get; set; }
    public string? IdentitySubject { get; set; }
    public string? IdentityIssuer { get; set; }
    public string? Email { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public UserRole Role { get; set; }
}
