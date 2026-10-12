using EnterpriseOMS.Application.Common.Exceptions;
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

namespace EnterpriseOMS.Application.Modules.Auth.Commands.Login
{
    public class LoginCommandHandler : IRequestHandler<LoginCommand, AuthResultDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IPasswordHasher _passwordHasher;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IDateTime _dateTime;

        public LoginCommandHandler(
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

        public async Task<AuthResultDto> Handle(LoginCommand request, CancellationToken cancellationToken)
        {
            var normalizedEmail = request.Email.Trim().ToUpperInvariant();

            var user = await _context.Users
                .Include(u => u.Role)
                    .ThenInclude(r => r.RolePermissions)
                        .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(u => u.NormalizedEmail == normalizedEmail, cancellationToken);

            // Deliberately identical error for "no such user" and "wrong password"
            // — prevents user-enumeration attacks via error-message differences.
            if (user is null || !_passwordHasher.Verify(user.PasswordHash, request.Password))
            {
                throw new ValidationException(
                    new[] { new FluentValidation.Results.ValidationFailure("Email", "Invalid email or password.") });
            }

            if (!user.IsActive)
            {
                throw new ForbiddenAccessException();
            }

            user.RecordLogin(_dateTime.UtcNow);

            var permissions = user.Role.RolePermissions
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
                Role = user.Role.Name,
                AccessToken = accessToken,
                RefreshToken = refreshTokenValue,
                AccessTokenExpiresAt = _dateTime.UtcNow.AddMinutes(_jwtTokenService.AccessTokenExpiryMinutes)
            };
        }
    }
}
