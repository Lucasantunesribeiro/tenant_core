using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Tasks.Commands;

public sealed record UpdateTaskStatusCommand(Guid TaskId, WorkTaskStatus Status) : IRequest;

internal sealed class UpdateTaskStatusCommandValidator : AbstractValidator<UpdateTaskStatusCommand>
{
    public UpdateTaskStatusCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
    }
}

internal sealed class UpdateTaskStatusCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IClock clock,
    IAuditService auditService) : IRequestHandler<UpdateTaskStatusCommand>
{
    public async Task Handle(UpdateTaskStatusCommand request, CancellationToken cancellationToken)
    {
        currentSession.EnsureAuthenticated();

        var task = await dbContext.Tasks.SingleOrDefaultAsync(x => x.Id == request.TaskId, cancellationToken)
            ?? throw new AppException("task_not_found", "Task not found", 404, "The requested task does not exist.");

        if (currentSession.Role == UserRole.User && task.AssigneeUserId != currentSession.UserId)
        {
            throw new AppException("forbidden", "Forbidden", 403, "Users can only update tasks assigned to them.");
        }

        task.UpdateStatus(request.Status, clock.UtcNow);

        await auditService.WriteAsync("task.status_updated", "Task", task.Id.ToString(), new { request.Status }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
