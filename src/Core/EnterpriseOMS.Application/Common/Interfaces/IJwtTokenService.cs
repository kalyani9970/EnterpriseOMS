using EnterpriseOMS.Domain.Entities.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Common.Interfaces
{
    public interface IJwtTokenService
    {
        string GenerateAccessToken(User user, IReadOnlyList<string> permissions);
        string GenerateRefreshToken();
        int AccessTokenExpiryMinutes { get; }
    }
}
