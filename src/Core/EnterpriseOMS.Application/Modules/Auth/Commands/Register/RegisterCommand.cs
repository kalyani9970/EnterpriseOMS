using EnterpriseOMS.Application.Modules.Auth.DTOs;
using MediatR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Auth.Commands.Register
{
    public class RegisterCommand : IRequest<AuthResultDto>
    {
        public string FullName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string Password { get; set; } = default!;
        public int OrganizationId { get; set; }

        // Defaults to a baseline "Employee" role if omitted; only elevated
        // roles should be assignable through an admin-only invite flow later,
        // not through open self-registration.
        public int? RoleId { get; set; }
    }
}
