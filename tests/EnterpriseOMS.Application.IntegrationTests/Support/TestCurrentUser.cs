using EnterpriseOMS.Application.Common.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Support
{
    /// <summary>Settable stand-in for the HttpContext-based current user.</summary>
    public sealed class TestCurrentUser : ICurrentUserService
    {
        public int? UserId { get; set; }
        public int? OrganizationId { get; set; }
        public string? Email { get; set; }
        public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
        public bool IsAuthenticated => UserId.HasValue;
    }
}
