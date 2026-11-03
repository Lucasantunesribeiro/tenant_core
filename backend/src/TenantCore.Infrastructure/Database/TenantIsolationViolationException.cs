namespace TenantCore.Infrastructure.Database;

/// <summary>
/// Lançada quando uma escrita tenta cruzar a fronteira de tenant (criar, alterar ou apagar
/// linha de outro tenant, ou mudar o TenantId de uma entidade existente). Indica bug de
/// aplicação: o handler deveria ter respondido 404 antes de chegar ao SaveChanges.
/// O GlobalExceptionMiddleware a converte em 500 genérico, sem expor detalhes.
/// </summary>
public sealed class TenantIsolationViolationException(string message) : InvalidOperationException(message);
