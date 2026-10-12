using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Common.Interfaces
{
    /// <summary>
    /// Reads the authenticated user's identity out of the current HTTP context.
    /// Implemented in Infrastructure (reads HttpContext.User claims) so that
    /// Application handlers can ask "who is doing this?" without referencing
    /// ASP.NET Core types directly.
    /// </summary>
    public interface ICurrentUserService
    {
        int? UserId { get; }
        int? OrganizationId { get; }
        string? Email { get; }
        IReadOnlyList<string> Permissions { get; }
        bool IsAuthenticated { get; }
    }
}
