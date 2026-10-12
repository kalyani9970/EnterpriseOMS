using EnterpriseOMS.Domain.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Domain.Entities.Identity
{
    public class User : BaseAuditableEntity, IAggregateRoot
    {
        public string FullName { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string NormalizedEmail { get; set; } = default!;
        public string PasswordHash { get; set; } = default!;
        public bool IsActive { get; set; } = true;
        public DateTime? LastLoginAt { get; set; }

        // Multi-tenancy boundary — every user belongs to exactly one organization.
        public int OrganizationId { get; set; }

        public int RoleId { get; set; }
        public Role Role { get; set; } = default!;

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

        public void Deactivate() => IsActive = false;
        public void RecordLogin(DateTime utcNow) => LastLoginAt = utcNow;
    }
}
