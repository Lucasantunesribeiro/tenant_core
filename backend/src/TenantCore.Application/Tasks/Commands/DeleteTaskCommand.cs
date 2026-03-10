using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;

namespace TenantCore.Application.Tasks.Commands;

public sealed record DeleteTaskCommand(Guid TaskId) : IRequest;

internal sealed class DeleteTaskCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IAuditService auditService,
    ICacheService cacheService) : IRequestHandler<DeleteTaskCommand>
{
    public async Task Handle(DeleteTaskCommand request, CancellationToken cancellationToken)
    {
        currentSession.EnsureManagerOrAdmin();

        var task = await dbContext.Tasks.SingleOrDefaultAsync(x => x.Id == request.TaskId, cancellationToken)
            ?? throw new AppException("task_not_found", "Task not found", 404, "The requested task does not exist.");

        dbContext.Tasks.Remove(task);

        await auditService.WriteAsync("task.deleted", "Task", task.Id.ToString(), new { task.Title }, cancellationToken);
        await cacheService.RemoveAsync($"usage:{task.TenantId}", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
