using EnterpriseOMS.Application.Common.Interfaces;
using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure.Identity
{
    /// <summary>
    /// Wraps ASP.NET Core Identity's PasswordHasher, which uses PBKDF2 with a per-password
    /// salt and an embedded format marker, so the algorithm can be upgraded later.
    /// We never roll our own crypto. The TUser type parameter is unused by the hasher,
    /// so we pass a throwaway object.
    /// </summary>
    public class PasswordHasherService : IPasswordHasher
    {
        private static readonly object Placeholder = new();
        private readonly PasswordHasher<object> _hasher = new();

        public string Hash(string password) => _hasher.HashPassword(Placeholder, password);

        public bool Verify(string hashedPassword, string providedPassword)
        {
            var result = _hasher.VerifyHashedPassword(Placeholder, hashedPassword, providedPassword);
            return result is PasswordVerificationResult.Success
                or PasswordVerificationResult.SuccessRehashNeeded;
        }
    }
}
