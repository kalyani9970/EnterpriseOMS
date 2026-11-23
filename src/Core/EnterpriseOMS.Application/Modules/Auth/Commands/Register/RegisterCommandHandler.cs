using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Common.Security;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using EnterpriseOMS.Application.Modules.Organizations;
using EnterpriseOMS.Domain.Entities.Identity;
using EnterpriseOMS.Domain.Entities.Tenancy;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Auth.Commands.Register
{
    public class RegisterCommandHandler : IRequestHandler<RegisterCommand, AuthResultDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IDateTime _dateTime;

        public RegisterCommandHandler(
            IApplicationDbContext context,
            IPasswordHasher passwordHasher,
            IJwtTokenService jwtTokenService,
            IDateTime dateTime)
        {
            _context = context;
            _passwordHasher = passwordHasher;
            _jwtTokenService = jwtTokenService;
            _dateTime = dateTime;
        }

        public async Task<AuthResultDto> Handle(RegisterCommand request, CancellationToken cancellationToken)
        {
            var normalizedEmail = request.Email.Trim().ToUpperInvariant();

            // IgnoreQueryFilters: sign-up spans ALL organizations (an email is unique platform-wide),
            // and must not depend on whether the caller happens to carry a token.
            var emailExists = await _context.Users
                .IgnoreQueryFilters()
                .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

            if (emailExists)
            {
                throw new ValidationException(
                    new[] { new FluentValidation.Results.ValidationFailure("Email", "This email is already registered.") });
            }

            var adminRole = await _context.Roles
                .Include(r => r.RolePermissions)
                    .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(r => r.Name == RoleNames.Admin, cancellationToken)
                ?? throw new NotFoundException("Default 'Admin' role not found. Seed data may be missing.");

            var organization = Organization.Create(
                request.OrganizationName,
                await GenerateUniqueCodeAsync(request.OrganizationName, cancellationToken));

            var user = new User
            {
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim(),
                NormalizedEmail = normalizedEmail,
                PasswordHash = _passwordHasher.Hash(request.Password),
                IsActive = true,
                Created = _dateTime.UtcNow,
                Organization = organization,   // EF fills in OrganizationId when both rows are saved
                Role = adminRole
            };

            _context.Organizations.Add(organization);
            _context.Users.Add(user);

            // One SaveChanges is one database transaction: the organization and its first user
            // are created together, or neither is.
            await _context.SaveChangesAsync(cancellationToken);

            var permissions = adminRole.RolePermissions
                .Select(rp => rp.Permission.Code)
                .ToList();

            var accessToken = _jwtTokenService.GenerateAccessToken(user, permissions);
            var refreshTokenValue = _jwtTokenService.GenerateRefreshToken();

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                Token = refreshTokenValue,
                CreatedAt = _dateTime.UtcNow,
                ExpiresAt = _dateTime.UtcNow.AddDays(7)
            });
            await _context.SaveChangesAsync(cancellationToken);

            return new AuthResultDto
            {
                UserId = user.Id,
                FullName = user.FullName,
                Email = user.Email,
                Role = adminRole.Name,
                AccessToken = accessToken,
                RefreshToken = refreshTokenValue,
                AccessTokenExpiresAt = _dateTime.UtcNow.AddMinutes(_jwtTokenService.AccessTokenExpiryMinutes)
            };
        }

        /// <summary>
        /// "acme" is taken? Try "acme-2", "acme-3"... The unique index on Organizations.Code is the
        /// real guarantee: two sign-ups racing for the same name could both pass this check, and the
        /// database would reject the second one.
        /// </summary>
        private async Task<string> GenerateUniqueCodeAsync(string organizationName, CancellationToken cancellationToken)
        {
            var baseCode = OrganizationCode.FromName(organizationName);
            var candidate = baseCode;
            var suffix = 2;

            while (await _context.Organizations
                       .IgnoreQueryFilters()
                       .AnyAsync(o => o.Code == candidate, cancellationToken))
            {
                candidate = $"{baseCode}-{suffix}";
                suffix++;
            }

            return candidate;
        }
    }
}
