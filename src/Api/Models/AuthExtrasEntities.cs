namespace Api.Models;

public enum ApiKeyScope { ReadOnly, ReadWrite, Admin }

public class Profile : BaseEntity
{
    public string Name        { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool   IsDefault   { get; set; }
    public ICollection<UserProfile> UserProfiles { get; set; } = [];
}

public class UserProfile : BaseEntity
{
    public string  UserId    { get; set; } = string.Empty; // IdentityUser.Id
    public Guid    ProfileId { get; set; }
    public bool    IsActive  { get; set; } = true;
    public Profile? Profile  { get; set; }
}

public class ApiKey : BaseEntity
{
    public string      UserId      { get; set; } = string.Empty;
    public string      Name        { get; set; } = string.Empty;
    public string      KeyHash     { get; set; } = string.Empty; // SHA-256 do valor real
    public string      Prefix      { get; set; } = string.Empty; // primeiros 8 chars (exibição)
    public ApiKeyScope Scope       { get; set; } = ApiKeyScope.ReadOnly;
    public bool        IsActive    { get; set; } = true;
    public DateTime?   ExpiresAt   { get; set; }
    public DateTime?   LastUsedAt  { get; set; }
    public Guid?       WorkspaceId { get; set; }

    public Workspace?  Workspace   { get; set; }
}
