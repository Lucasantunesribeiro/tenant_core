using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using TenantCore.Application.Common.Abstractions;
using TenantCore.Application.Common.Exceptions;
using TenantCore.Domain.Enums;

namespace TenantCore.Application.Clients.Commands;

public sealed record UpdateClientCommand(
    Guid ClientId,
    string Name,
    string Email,
    string ContactName,
    ClientStatus Status,
    string Notes) : IRequest;

internal sealed class UpdateClientCommandValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientCommandValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.ContactName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Notes).MaximumLength(500);
    }
}

internal sealed class UpdateClientCommandHandler(
    ITenantCoreDbContext dbContext,
    ICurrentSession currentSession,
    IClock clock,
    IAuditService auditService) : IRequestHandler<UpdateClientCommand>
{
    public async Task Handle(UpdateClientCommand request, CancellationToken cancellationToken)
    {
        currentSession.EnsureManagerOrAdmin();

        var client = await dbContext.Clients.SingleOrDefaultAsync(x => x.Id == request.ClientId, cancellationToken)
            ?? throw new AppException("client_not_found", "Client not found", 404, "The requested client does not exist.");

        client.Update(
            request.Name.Trim(),
            request.Email.Trim().ToLowerInvariant(),
            request.ContactName.Trim(),
            request.Status,
            request.Notes.Trim(),
            clock.UtcNow);

        await auditService.WriteAsync("client.updated", "Client", client.Id.ToString(), request, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
