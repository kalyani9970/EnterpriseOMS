using EnterpriseOMS.Application.Common.Security;
using EnterpriseOMS.Application.Modules.Roles.DTOs;
using EnterpriseOMS.Application.Modules.Roles.Queries.GetRoles;
using EnterpriseOMS.Infrastructure.Authorization;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace EnterpriseOMS.Api.Controllers
{
    [ApiController]
    [Route("api/roles")]
    [Produces("application/json")]
    public class RolesController : ControllerBase
    {
        private readonly ISender _sender;

        public RolesController(ISender sender)
        {
            _sender = sender;
        }

        /// <summary>Lists all roles with their permission codes. Requires "roles.view".</summary>
        [HttpGet]
        [HasPermission(Permissions.Roles.View)]
        [ProducesResponseType(typeof(List<RoleDto>), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<List<RoleDto>>> GetAll(CancellationToken cancellationToken)
        {
            return Ok(await _sender.Send(new GetRolesQuery(), cancellationToken));
        }
    }
}
