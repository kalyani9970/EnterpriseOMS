using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Organizations
{
    /// <summary>
    /// Turns an organization name into the URL-safe base of its unique code:
    /// "Acme Traders, Inc." becomes "acme-traders-inc". Uniqueness (appending -2, -3...) is
    /// handled by the registration handler, because it needs the database.
    /// Only ASCII letters and digits survive, so a name written entirely in another script
    /// falls back to "org" (the handler then makes it unique: org, org-2, org-3...).
    /// </summary>
    public static class OrganizationCode
    {
        /// <summary>Leaves room for a "-NNNNNNNNN" suffix inside the 50-character code limit.</summary>
        public const int BaseMaxLength = 40;

        private static readonly Regex NonAlphanumeric = new("[^a-z0-9]+", RegexOptions.Compiled);

        public static string FromName(string name)
        {
            var slug = NonAlphanumeric.Replace(name.Trim().ToLowerInvariant(), "-").Trim('-');

            if (slug.Length > BaseMaxLength)
            {
                slug = slug[..BaseMaxLength].TrimEnd('-');
            }

            return slug.Length == 0 ? "org" : slug;
        }
    }
}
