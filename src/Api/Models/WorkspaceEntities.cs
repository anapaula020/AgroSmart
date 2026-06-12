namespace Api.Models;

public enum WorkspaceRole { Owner, Agronomo, Tecnico, Produtor }
public enum InviteStatus { Pending, Accepted, Cancelled }

public class Workspace : BaseEntity
{
    public string OwnerId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }

    public ICollection<WorkspaceMember> Members { get; set; } = [];
    public ICollection<WorkspaceInvite> Invites { get; set; } = [];
    public ICollection<RuralProperty> Properties { get; set; } = [];
    public ICollection<ApiKey> ApiKeys { get; set; } = [];
}

public class WorkspaceMember : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public WorkspaceRole Role { get; set; } = WorkspaceRole.Produtor;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Workspace? Workspace { get; set; }
}

public class WorkspaceInvite : BaseEntity
{
    public Guid WorkspaceId { get; set; }
    public string InvitedEmail { get; set; } = string.Empty;
    public WorkspaceRole Role { get; set; } = WorkspaceRole.Tecnico;
    public string Token { get; set; } = string.Empty;
    public InviteStatus Status { get; set; } = InviteStatus.Pending;
    public string? InvitedByUserId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public DateTime? AcceptedAt { get; set; }

    public Workspace? Workspace { get; set; }
}
