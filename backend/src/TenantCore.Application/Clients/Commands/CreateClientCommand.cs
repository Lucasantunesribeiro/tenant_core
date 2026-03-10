using FluentValidation;
using MediatR;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Domain.Entities;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Clients.Commands;

public sealed record CreateClientCommand(
    string Name,
    string Email,
    string ContactName,
    ClientStatus Status,
    string Notes) : IRequest<Guid>;

internal sealed class CreateClientCommandValidator : AbstractValidator<CreateClientCommand>
{
    public CreateClientCommandValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.ContactName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

internal sealed class CreateClientCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IPlanLimitService planLimitService,
    IAuditService auditService,
    ICacheService cacheService) : IRequestHandler<CreateClientCommand, Guid>
{
    public async Task<Guid> Handle(CreateClientCommand request, CancellationToken cancellationToken)
    {
        currentSession.EnsureManagerOrAdmin();
        await planLimitService.EnsureClientSlotAvailableAsync(cancellationToken);

        var client = new Client(
            currentSession.GetRequiredTenantId(),
            request.Name.Trim(),
            request.Email.Trim().ToLowerInvariant(),
            request.ContactName.Trim(),
            request.Status,
            request.Notes.Trim());

        await dbContext.Clients.AddAsync(client, cancellationToken);
        await auditService.WriteAsync("client.created", "Client", client.Id.ToString(), request, cancellationToken);
        await cacheService.RemoveAsync($"usage:{client.TenantId}", cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return client.Id;
    }
}
