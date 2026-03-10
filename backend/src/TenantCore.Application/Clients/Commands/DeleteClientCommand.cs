using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;

namespace TenantCore.Application.Clients.Commands;

public sealed record DeleteClientCommand(Guid ClientId) : IRequest;

internal sealed class DeleteClientCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IAuditService auditService,
    ICacheService cacheService) : IRequestHandler<DeleteClientCommand>
{
    public async Task Handle(DeleteClientCommand request, CancellationToken cancellationToken)
    {
        currentSession.EnsureManagerOrAdmin();

        var client = await dbContext.Clients.SingleOrDefaultAsync(x => x.Id == request.ClientId, cancellationToken)
            ?? throw new AppException("client_not_found", "Client not found", 404, "The requested client does not exist.");

        dbContext.Clients.Remove(client);

        await auditService.WriteAsync("client.deleted", "Client", client.Id.ToString(), new { client.Name }, cancellationToken);
        await cacheService.RemoveAsync($"usage:{client.TenantId}", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
