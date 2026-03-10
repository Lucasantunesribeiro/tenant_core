using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Tasks.Commands;

public sealed record UpdateTaskCommand(
    Guid TaskId,
    Guid ProjectId,
    Guid? AssigneeUserId,
    string Title,
    string Description,
    WorkTaskStatus Status,
    TaskPriority Priority,
    DateOnly? DueDate) : IRequest;

internal sealed class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(x => x.TaskId).NotEmpty();
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Title).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Description).MaximumLength(600);
    }
}

internal sealed class UpdateTaskCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IClock clock,
    IAuditService auditService) : IRequestHandler<UpdateTaskCommand>
{
    public async Task Handle(UpdateTaskCommand request, CancellationToken cancellationToken)
    {
        currentSession.EnsureManagerOrAdmin();

        var task = await dbContext.Tasks.SingleOrDefaultAsync(x => x.Id == request.TaskId, cancellationToken)
            ?? throw new AppException("task_not_found", "Task not found", 404, "The requested task does not exist.");

        task.Update(
            request.ProjectId,
            request.AssigneeUserId,
            request.Title.Trim(),
            request.Description.Trim(),
            request.Status,
            request.Priority,
            request.DueDate,
            clock.UtcNow);

        await auditService.WriteAsync("task.updated", "Task", task.Id.ToString(),
            new { request.Title, request.Status, request.Priority, request.ProjectId, request.AssigneeUserId }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
