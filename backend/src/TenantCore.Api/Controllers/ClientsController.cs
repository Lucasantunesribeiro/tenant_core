using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using TenantCore.Api.Common;
using TenantCore.Application.Clients.Commands;
using TenantCore.Application.Clients.Queries;
using TenantCore.Application.Common.Models;
using TenantCore.Application.Common.Security;
using TenantCore.Domain.Enums;

namespace TenantCore.Api.Controllers;

[Authorize(Policy = PolicyNames.TenantMember)]
[EnableRateLimiting(RateLimitPolicyNames.Api)]
[Route("api/clients")]
public sealed class ClientsController : ApiControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<ClientListItem>>> GetClients(
        [FromQuery] string? search,
        [FromQuery] ClientStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        return Ok(await Sender.Send(new GetClientsQuery(search, status, page, pageSize), cancellationToken));
    }

    [Authorize(Policy = PolicyNames.ManagerOrAdmin)]
    [HttpPost]
    public async Task<ActionResult<Guid>> CreateClient([FromBody] ClientRequest request, CancellationToken cancellationToken)
    {
        var clientId = await Sender.Send(
            new CreateClientCommand(request.Name, request.Email, request.ContactName, request.Status, request.Notes),
            cancellationToken);

        return CreatedAtAction(nameof(GetClients), new { id = clientId }, clientId);
    }

    [Authorize(Policy = PolicyNames.ManagerOrAdmin)]
    [HttpPut("{clientId:guid}")]
    public async Task<IActionResult> UpdateClient(Guid clientId, [FromBody] ClientRequest request, CancellationToken cancellationToken)
    {
        await Sender.Send(
            new UpdateClientCommand(clientId, request.Name, request.Email, request.ContactName, request.Status, request.Notes),
            cancellationToken);

        return NoContent();
    }

    [Authorize(Policy = PolicyNames.ManagerOrAdmin)]
    [HttpDelete("{clientId:guid}")]
    public async Task<IActionResult> DeleteClient(Guid clientId, CancellationToken cancellationToken)
    {
        await Sender.Send(new DeleteClientCommand(clientId), cancellationToken);
        return NoContent();
    }
}

public sealed record ClientRequest(
    string Name,
    string Email,
    string ContactName,
    ClientStatus Status,
    string Notes);
