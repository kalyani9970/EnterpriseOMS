using EnterpriseOMS.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace EnterpriseOMS.Api.Extensions
{
    public static class JwtExtensions
    {
        public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
        {
            var jwt = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
                      ?? throw new InvalidOperationException("The 'Jwt' configuration section is missing.");

            if (string.IsNullOrWhiteSpace(jwt.Secret) || jwt.Secret.Length < 32)
                throw new InvalidOperationException(
                    "Jwt:Secret is missing or shorter than 32 characters. Set it with 'dotnet user-secrets'.");

            services
                .AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(options =>
                {
                    // Keep claim names exactly as issued ("sub", "role", "permission").
                    // Without this, the handler renames "sub" to a long ClaimTypes.NameIdentifier URI.
                    options.MapInboundClaims = false;

                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),

                        ValidateIssuer = true,
                        ValidIssuer = jwt.Issuer,

                        ValidateAudience = true,
                        ValidAudience = jwt.Audience,

                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),

                        RoleClaimType = CustomClaimTypes.Role,
                        NameClaimType = CustomClaimTypes.FullName
                    };
                });

            // The custom PermissionPolicyProvider and handler were registered in AddInfrastructure.
            services.AddAuthorization();

            return services;
        }
    }
}
