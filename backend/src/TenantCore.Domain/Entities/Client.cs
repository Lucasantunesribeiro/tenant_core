using TenantCore.Domain.Common;
using TenantCore.Domain.Enums;

namespace TenantCore.Domain.Entities;

public sealed class Client : TenantOwnedEntity
{
    private Client()
    {
    }

    public Client(
        Guid tenantId,
        string name,
        string email,
        string contactName,
        ClientStatus status,
        string notes)
    {
        TenantId = tenantId;
        Name = name;
        Email = email;
        ContactName = contactName;
        Status = status;
        Notes = notes;
    }

    public string Name { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string ContactName { get; private set; } = string.Empty;

    public ClientStatus Status { get; private set; }

    public string Notes { get; private set; } = string.Empty;

    public void Update(
        string name,
        string email,
        string contactName,
        ClientStatus status,
        string notes,
        DateTimeOffset now)
    {
        Name = name;
        Email = email;
        ContactName = contactName;
        Status = status;
        Notes = notes;
        Touch(now);
    }
}
