using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using EnterpriseOMS.Domain.Entities.Identity;
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

            var emailExists = await _context.Users
                .AnyAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

            if (emailExists)
            {
                throw new Common.Exceptions.ValidationException(
                    new[] { new FluentValidation.Results.ValidationFailure("Email", "This email is already registered.") });
            }

            // Default new self-registrations to the baseline "Employee" role
            // unless explicitly provided (e.g., by an admin invite flow).
            var roleId = request.RoleId;
            if (roleId is null)
            {
                var defaultRole = await _context.Roles
                    .FirstOrDefaultAsync(r => r.Name == "Employee", cancellationToken)
                    ?? throw new Common.Exceptions.NotFoundException("Default 'Employee' role not found. Seed data may be missing.");
                roleId = defaultRole.Id;
            }

            var user = new User
            {
                FullName = request.FullName.Trim(),
                Email = request.Email.Trim(),
                NormalizedEmail = normalizedEmail,
                PasswordHash = _passwordHasher.Hash(request.Password),
                OrganizationId = request.OrganizationId,
                RoleId = roleId.Value,
                IsActive = true,
                Created = _dateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync(cancellationToken);

            // Reload with Role + Permissions to build JWT claims accurately.
            var savedUser = await _context.Users
                .Include(u => u.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .FirstAsync(u => u.Id == user.Id, cancellationToken);

            var permissions = savedUser.Role.RolePermissions
                .Select(rp => rp.Permission.Code)
                .ToList();

            var accessToken = _jwtTokenService.GenerateAccessToken(savedUser, permissions);
            var refreshTokenValue = _jwtTokenService.GenerateRefreshToken();

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = savedUser.Id,
                Token = refreshTokenValue,
                CreatedAt = _dateTime.UtcNow,
                ExpiresAt = _dateTime.UtcNow.AddDays(7)
            });
            await _context.SaveChangesAsync(cancellationToken);

            return new AuthResultDto
            {
                UserId = savedUser.Id,
                FullName = savedUser.FullName,
                Email = savedUser.Email,
                Role = savedUser.Role.Name,
                AccessToken = accessToken,
                RefreshToken = refreshTokenValue,
                AccessTokenExpiresAt = _dateTime.UtcNow.AddMinutes(_jwtTokenService.AccessTokenExpiryMinutes)
            };
        }
    }
}
