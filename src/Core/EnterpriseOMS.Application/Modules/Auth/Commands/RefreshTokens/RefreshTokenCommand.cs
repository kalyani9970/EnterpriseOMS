using EnterpriseOMS.Application.Modules.Auth.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Auth.Commands.RefreshTokens
{
    public class RefreshTokenCommand : IRequest<AuthResultDto>
    {
        public string RefreshToken { get; set; } = default!;
    }
}
