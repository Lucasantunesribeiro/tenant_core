using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace TenantCore.Api.Common;

[ApiController]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected ISender Sender => HttpContext.RequestServices.GetRequiredService<ISender>();
}
