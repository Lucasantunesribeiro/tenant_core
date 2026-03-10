namespace TenantCore.Domain.Common;

public abstract class AuditableEntity : Entity
{
    public DateTimeOffset CreatedAtUtc { get; protected set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; protected set; } = DateTimeOffset.UtcNow;

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public void Touch(DateTimeOffset now)
    {
        UpdatedAtUtc = now;
    }
}
