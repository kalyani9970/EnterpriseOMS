using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using MediatR;

namespace EnterpriseOMS.Application.Modules.Auth.Commands.Register;

/// <summary>
/// Self-service sign-up: creates a NEW organization and its first user, who becomes that
/// organization's Admin. There is intentionally no OrganizationId or RoleId here. Letting a
/// caller pick an existing organization would let anyone join any company, and letting them
/// pick a role would let anyone make themselves an admin. Further users are added by an
/// organization admin (Users module).
/// It is a transactional command: the handler saves in two steps (organization + user, then the
/// refresh token), and the pipeline now makes those steps all-or-nothing.
/// </summary>
public class RegisterCommand : IRequest<AuthResultDto>, ITransactionalCommand
{
    public string FullName { get; set; } = default!;
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string OrganizationName { get; set; } = default!;
}