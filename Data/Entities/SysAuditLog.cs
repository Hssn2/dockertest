namespace dockertest.Data.Entities;

public class SysAuditLog
{
    public Guid Id { get; set; }

    public string Action { get; set; } = string.Empty;

    public string? EntityName { get; set; }

    public string? EntityId { get; set; }

    public Guid? UserId { get; set; }

    public string? Details { get; set; }

    public DateTime CreatedAt { get; set; }
}
