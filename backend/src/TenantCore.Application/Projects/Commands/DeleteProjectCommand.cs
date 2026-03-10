using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;

namespace TenantCore.Application.Projects.Commands;

public sealed record DeleteProjectCommand(Guid ProjectId) : IRequest;

internal sealed class DeleteProjectCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IAuditService auditService,
    ICacheService cacheService) : IRequestHandler<DeleteProjectCommand>
{
    public async Task Handle(DeleteProjectCommand request, CancellationToken cancellationToken)
    {
        currentSession.EnsureManagerOrAdmin();

        var project = await dbContext.Projects.SingleOrDefaultAsync(x => x.Id == request.ProjectId, cancellationToken)
            ?? throw new AppException("project_not_found", "Project not found", 404, "The requested project does not exist.");

        dbContext.Projects.Remove(project);

        await auditService.WriteAsync("project.deleted", "Project", project.Id.ToString(), new { project.Name }, cancellationToken);
        await cacheService.RemoveAsync($"usage:{project.TenantId}", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
