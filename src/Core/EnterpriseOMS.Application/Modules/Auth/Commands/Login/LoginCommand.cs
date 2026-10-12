using EnterpriseOMS.Application.Modules.Auth.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Auth.Commands.Login
{
    public class LoginCommand : IRequest<AuthResultDto>
    {
        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
    }
}
