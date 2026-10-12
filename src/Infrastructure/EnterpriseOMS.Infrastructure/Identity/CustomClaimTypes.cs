using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure.Identity
{
    /// <summary>Single source of truth for the custom claim names embedded in our JWTs.</summary>
    public static class CustomClaimTypes
    {
        public const string Permission = "permission";
        public const string OrganizationId = "org_id";
        public const string FullName = "full_name";
        public const string Role = "role";
    }
}
