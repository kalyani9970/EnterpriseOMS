using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Domain.Entities.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Infrastructure.Identity
{
    public class JwtTokenService : IJwtTokenService
    {
        private readonly JwtSettings _settings;
        private readonly IDateTime _dateTime;

        public JwtTokenService(IOptions<JwtSettings> options, IDateTime dateTime)
        {
            _settings = options.Value;
            _dateTime = dateTime;

            if (string.IsNullOrWhiteSpace(_settings.Secret) || _settings.Secret.Length < 32)
                throw new InvalidOperationException(
                    "Jwt:Secret must be configured and at least 32 characters long (HMAC-SHA256 key strength).");
        }

        public int AccessTokenExpiryMinutes => _settings.AccessTokenExpiryMinutes;

        /// <summary>
        /// Requires user.Role to be loaded. Permissions are flattened into one "permission"
        /// claim each, so authorization needs no database hit per request.
        /// Trade-off: permission changes take effect when the token is refreshed, which is
        /// why access tokens are short-lived (15 min default).
        /// </summary>
        public string GenerateAccessToken(User user, IReadOnlyList<string> permissions)
        {
            var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(CustomClaimTypes.FullName, user.FullName),
            new(CustomClaimTypes.OrganizationId, user.OrganizationId.ToString()),
            new(CustomClaimTypes.Role, user.Role.Name)
        };

            claims.AddRange(permissions.Select(p => new Claim(CustomClaimTypes.Permission, p)));

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_settings.Secret));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var now = _dateTime.UtcNow;

            var token = new JwtSecurityToken(
                issuer: _settings.Issuer,
                audience: _settings.Audience,
                claims: claims,
                notBefore: now,
                expires: now.AddMinutes(_settings.AccessTokenExpiryMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>Refresh tokens are opaque random strings (not JWTs): 64 bytes from a CSPRNG.</summary>
        public string GenerateRefreshToken()
        {
            var randomBytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(randomBytes);
        }
    }
}
