using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure.Services
{
    /// <summary>
    /// Reads the authenticated user's identity from the JWT claims on the current request.
    /// Registered as Scoped: it is created once per HTTP request and reads HttpContext lazily.
    /// IHttpContextAccessor is the supported way to reach HttpContext outside controllers.
    /// </summary>
    public class CurrentUserService : ICurrentUserService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public CurrentUserService(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

        public int? UserId
        {
            get
            {
                // "sub" when MapInboundClaims = false (our setup); NameIdentifier is a safe fallback.
                var value = Principal?.FindFirstValue(JwtRegisteredClaimNames.Sub)
                            ?? Principal?.FindFirstValue(ClaimTypes.NameIdentifier);

                return int.TryParse(value, out var id) ? id : null;
            }
        }

        public int? OrganizationId =>
            int.TryParse(Principal?.FindFirstValue(CustomClaimTypes.OrganizationId), out var orgId)
                ? orgId
                : null;

        public string? Email =>
            Principal?.FindFirstValue(JwtRegisteredClaimNames.Email)
            ?? Principal?.FindFirstValue(ClaimTypes.Email);

        public IReadOnlyList<string> Permissions =>
            Principal?.FindAll(CustomClaimTypes.Permission).Select(c => c.Value).ToList()
            ?? new List<string>();

        public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;
    }
}
