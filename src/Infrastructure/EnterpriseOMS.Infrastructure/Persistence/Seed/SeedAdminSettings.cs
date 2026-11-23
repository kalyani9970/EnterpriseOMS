using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure.Persistence.Seed
{
    /// <summary>
    /// Bootstrap admin account, bound from the "SeedAdmin" config section.
    /// If Email or Password is empty, no admin user is seeded.
    /// The admin is placed in the seeded "Default Organization". The old OrganizationId setting
    /// no longer exists (you can delete it from appsettings.json).
    /// </summary>
    public class SeedAdminSettings
    {
        public const string SectionName = "SeedAdmin";

        public string FullName { get; set; } = "System Administrator";
        public string Email { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
