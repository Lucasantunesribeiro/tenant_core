using TenantCore.Domain.Common;

namespace TenantCore.Domain.Entities;

public sealed class Tenant : AuditableEntity
{
    private Tenant()
    {
    }

    public Tenant(string name, string slug, string billingEmail, string timeZone)
    {
        Name = name;
        Slug = slug;
        BillingEmail = billingEmail;
        TimeZone = timeZone;
        Theme = "industrial";
        SupportEmail = billingEmail;
        AllowedDomains = string.Empty;
        IsActive = true;
    }

    public string Name { get; private set; } = string.Empty;

    public string Slug { get; private set; } = string.Empty;

    public string BillingEmail { get; private set; } = string.Empty;

    public string SupportEmail { get; private set; } = string.Empty;

    public string TimeZone { get; private set; } = "UTC";

    public string Theme { get; private set; } = "industrial";

    public string AllowedDomains { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public void UpdateSettings(
        string name,
        string billingEmail,
        string supportEmail,
        string timeZone,
        string theme,
        string allowedDomains,
        DateTimeOffset now)
    {
        Name = name;
        BillingEmail = billingEmail;
        SupportEmail = supportEmail;
        TimeZone = timeZone;
        Theme = theme;
        AllowedDomains = allowedDomains;
        Touch(now);
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        Touch(now);
    }
}
