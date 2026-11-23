using EnterpriseOMS.Application.Common.Security;
using EnterpriseOMS.Application.Modules.Organizations.Commands.UpdateMyOrganization;
using EnterpriseOMS.Application.Modules.Organizations.DTOs;
using EnterpriseOMS.Application.Modules.Organizations.Queries.GetMyOrganization;
using EnterpriseOMS.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseOMS.Api.Controllers;

/// <summary>
/// Self-service for the caller's OWN organization. The route is "current" rather than "{id}" on
/// purpose: the organization always comes from the caller's token, so there is no id to tamper with.
/// </summary>
[ApiController]
[Route("api/organizations")]
[Produces("application/json")]
public class OrganizationsController : ControllerBase
{
    private readonly ISender _sender;

    public OrganizationsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>Returns the signed-in user's organization. Requires "organizations.view".</summary>
    [HttpGet("current")]
    [HasPermission(Permissions.Organizations.View)]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganizationDto>> GetCurrent(CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(new GetMyOrganizationQuery(), cancellationToken));
    }

    /// <summary>Renames the signed-in user's organization. Requires "organizations.update".</summary>
    [HttpPut("current")]
    [HasPermission(Permissions.Organizations.Update)]
    [ProducesResponseType(typeof(OrganizationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<OrganizationDto>> UpdateCurrent(
        [FromBody] UpdateMyOrganizationCommand command,
        CancellationToken cancellationToken)
    {
        return Ok(await _sender.Send(command, cancellationToken));
    }
}