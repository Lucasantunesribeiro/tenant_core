using System.Diagnostics;

namespace TenantCore.Infrastructure.Observability;

public static class TenantCoreTelemetry
{
    public static readonly ActivitySource ActivitySource = new("TenantCore");
}
