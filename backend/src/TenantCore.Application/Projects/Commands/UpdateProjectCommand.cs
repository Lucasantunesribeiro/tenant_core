using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Projects.Commands;

public sealed record UpdateProjectCommand(
    Guid ProjectId,
    Guid? ClientId,
    Guid? OwnerUserId,
    string Name,
    string Code,
    string Description,
    ProjectStatus Status,
    DateOnly? StartDate,
    DateOnly? DueDate) : IRequest;

internal sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(x => x.ProjectId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(600);
    }
}

internal sealed class UpdateProjectCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IClock clock,
    IAuditService auditService) : IRequestHandler<UpdateProjectCommand>
{
    public async Task Handle(UpdateProjectCommand request, CancellationToken cancellationToken)
    {
        currentSession.EnsureManagerOrAdmin();

        var project = await dbContext.Projects.SingleOrDefaultAsync(x => x.Id == request.ProjectId, cancellationToken)
            ?? throw new AppException("project_not_found", "Project not found", 404, "The requested project does not exist.");

        // ClientId/OwnerUserId vêm do corpo da requisição: as consultas abaixo passam pelo
        // query filter de tenant, então um id de outro tenant resulta em 404 (igual ao Create).
        if (request.ClientId.HasValue)
        {
            var clientExists = await dbContext.Clients.AnyAsync(x => x.Id == request.ClientId.Value, cancellationToken);
            if (!clientExists)
            {
                throw new AppException("client_not_found", "Client not found", 404, "The selected client does not exist.");
            }
        }

        if (request.OwnerUserId.HasValue)
        {
            var ownerExists = await dbContext.Users.AnyAsync(x => x.Id == request.OwnerUserId.Value, cancellationToken);
            if (!ownerExists)
            {
                throw new AppException("owner_not_found", "Owner not found", 404, "The selected owner does not exist.");
            }
        }

        var code = request.Code.Trim().ToUpperInvariant();
        var duplicate = await dbContext.Projects.AnyAsync(x => x.Id != request.ProjectId && x.Code == code, cancellationToken);
        if (duplicate)
        {
            throw new AppException("project_conflict", "Project already exists", 409, "A project with this code already exists.");
        }

        project.Update(
            request.ClientId,
            request.OwnerUserId,
            request.Name.Trim(),
            code,
            request.Description.Trim(),
            request.Status,
            request.StartDate,
            request.DueDate,
            clock.UtcNow);

        await auditService.WriteAsync("project.updated", "Project", project.Id.ToString(),
            new { request.Name, request.Code, request.Status, request.ClientId, request.OwnerUserId }, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
