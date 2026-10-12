using EnterpriseOMS.Application.IntegrationTests.Support;
using EnterpriseOMS.Domain.Entities.Identity;
using EnterpriseOMS.Infrastructure.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.IntegrationTests.Security
{
    public class JwtTokenServiceTests
    {
        private const string Secret = "0123456789-0123456789-0123456789-0123456789";

        private static JwtTokenService CreateService(FakeDateTime? clock = null, string secret = Secret, int expiryMinutes = 15)
        {
            var settings = new JwtSettings
            {
                Secret = secret,
                Issuer = "test-issuer",
                Audience = "test-audience",
                AccessTokenExpiryMinutes = expiryMinutes
            };

            return new JwtTokenService(Options.Create(settings), clock ?? new FakeDateTime());
        }

        private static User CreateUser() => new()
        {
            Id = 7,
            FullName = "Jane Doe",
            Email = "jane@example.com",
            OrganizationId = 3,
            Role = new Role { Id = 2, Name = "Admin" }
        };

        [Theory]
        [InlineData("")]
        [InlineData("too-short")]
        [InlineData("0123456789-0123456789-012345")]   // 29 characters
        public void Constructor_WeakSecret_Throws(string secret)
        {
            Assert.Throws<InvalidOperationException>(() => CreateService(secret: secret));
        }

        [Fact]
        public void GenerateAccessToken_ContainsIdentityClaims()
        {
            var token = CreateService().GenerateAccessToken(CreateUser(), new[] { "users.view" });

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            Assert.Equal("7", jwt.Claims.Single(c => c.Type == "sub").Value);
            Assert.Equal("jane@example.com", jwt.Claims.Single(c => c.Type == "email").Value);
            Assert.Equal("Jane Doe", jwt.Claims.Single(c => c.Type == "full_name").Value);
            Assert.Equal("3", jwt.Claims.Single(c => c.Type == "org_id").Value);
            Assert.Equal("Admin", jwt.Claims.Single(c => c.Type == "role").Value);
            Assert.Equal("test-issuer", jwt.Issuer);
            Assert.Contains("test-audience", jwt.Audiences);
        }

        [Fact]
        public void GenerateAccessToken_EmitsOnePermissionClaimPerPermission()
        {
            var permissions = new[] { "users.view", "users.create", "roles.view" };

            var token = CreateService().GenerateAccessToken(CreateUser(), permissions);

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            var emitted = jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value).ToList();

            Assert.Equal(permissions.OrderBy(p => p), emitted.OrderBy(p => p));
        }

        [Fact]
        public void GenerateAccessToken_HasUniqueJtiPerToken()
        {
            var service = CreateService();

            var first = new JwtSecurityTokenHandler().ReadJwtToken(service.GenerateAccessToken(CreateUser(), Array.Empty<string>()));
            var second = new JwtSecurityTokenHandler().ReadJwtToken(service.GenerateAccessToken(CreateUser(), Array.Empty<string>()));

            Assert.NotEqual(first.Id, second.Id);
        }

        [Fact]
        public void GenerateAccessToken_ExpiresAfterConfiguredMinutes()
        {
            var clock = new FakeDateTime();
            var token = CreateService(clock, expiryMinutes: 20).GenerateAccessToken(CreateUser(), Array.Empty<string>());

            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

            // JWT timestamps have whole-second precision, so allow a small tolerance.
            var expected = clock.UtcNow.AddMinutes(20);
            Assert.True(Math.Abs((jwt.ValidTo - expected).TotalSeconds) < 2);
        }

        [Fact]
        public void GenerateAccessToken_ValidatesWithCorrectKey_AndKeepsClaimNames()
        {
            var token = CreateService().GenerateAccessToken(CreateUser(), new[] { "users.view", "roles.view" });

            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
            var principal = handler.ValidateToken(token, BuildValidationParameters(Secret), out _);

            Assert.Equal(2, principal.FindAll("permission").Count());
            Assert.Equal("7", principal.FindFirst("sub")!.Value);
        }

        [Fact]
        public void GenerateAccessToken_FailsValidationWithWrongKey()
        {
            var token = CreateService().GenerateAccessToken(CreateUser(), Array.Empty<string>());
            var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };

            Assert.ThrowsAny<SecurityTokenException>(
                () => handler.ValidateToken(token, BuildValidationParameters("another-secret-another-secret-another-secret-123"), out _));
        }

        [Fact]
        public void GenerateRefreshToken_IsLongRandomAndUnique()
        {
            var service = CreateService();

            var first = service.GenerateRefreshToken();
            var second = service.GenerateRefreshToken();

            Assert.NotEqual(first, second);
            Assert.Equal(88, first.Length);   // 64 random bytes in Base64
        }

        [Fact]
        public void AccessTokenExpiryMinutes_ExposesConfiguredValue()
        {
            Assert.Equal(30, CreateService(expiryMinutes: 30).AccessTokenExpiryMinutes);
        }

        private static TokenValidationParameters BuildValidationParameters(string secret) => new()
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = "test-issuer",
            ValidateAudience = true,
            ValidAudience = "test-audience",
            ValidateLifetime = true,
            RoleClaimType = "role"
        };
    }
}
