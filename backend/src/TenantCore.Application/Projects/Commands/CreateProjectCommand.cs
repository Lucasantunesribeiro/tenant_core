using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Projects.Commands;

public sealed record CreateProjectCommand(
    Guid? ClientId,
    Guid? OwnerUserId,
    string Name,
    string Code,
    string Description,
    ProjectStatus Status,
    DateOnly? StartDate,
    DateOnly? DueDate) : IRequest<Guid>;

internal sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Description).MaximumLength(600);
    }
}

internal sealed class CreateProjectCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IPlanLimitService planLimitService,
    IAuditService auditService,
    ICacheService cacheService) : IRequestHandler<CreateProjectCommand, Guid>
{
    public async Task<Guid> Handle(CreateProjectCommand request, CancellationToken cancellationToken)
    {
        currentSession.EnsureManagerOrAdmin();
        await planLimitService.EnsureProjectSlotAvailableAsync(cancellationToken);

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
        var exists = await dbContext.Projects.AnyAsync(x => x.Code == code, cancellationToken);
        if (exists)
        {
            throw new AppException("project_conflict", "Project already exists", 409, "A project with this code already exists.");
        }

        var project = new Project(
            currentSession.GetRequiredTenantId(),
            request.ClientId,
            request.OwnerUserId,
            request.Name.Trim(),
            code,
            request.Description.Trim(),
            request.Status,
            request.StartDate,
            request.DueDate);

        await dbContext.Projects.AddAsync(project, cancellationToken);
        await auditService.WriteAsync("project.created", "Project", project.Id.ToString(),
            new { request.Name, request.Code, request.Status, request.ClientId, request.OwnerUserId }, cancellationToken);
        await cacheService.RemoveAsync($"usage:{project.TenantId}", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return project.Id;
    }
}
