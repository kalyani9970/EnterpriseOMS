using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure.Identity
{
    /// <summary>
    /// Strongly-typed binding for the "Jwt" section of appsettings.json (the Options pattern).
    /// The secret must NEVER be committed for real environments: use user-secrets locally
    /// and environment variables / a vault in production.
    /// </summary>
    public class JwtSettings
    {
        public const string SectionName = "Jwt";

        public string Secret { get; set; } = default!;
        public string Issuer { get; set; } = default!;
        public string Audience { get; set; } = default!;
        public int AccessTokenExpiryMinutes { get; set; } = 15;
    }
}
