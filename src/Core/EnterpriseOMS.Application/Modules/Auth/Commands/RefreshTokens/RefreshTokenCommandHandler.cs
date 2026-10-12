using EnterpriseOMS.Application.Common.Exceptions;
using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Modules.Auth.Commands.RefreshTokens;
using EnterpriseOMS.Application.Modules.Auth.DTOs;
using EnterpriseOMS.Domain.Entities.Identity;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Auth.Commands.RefreshTokens
{
    public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, AuthResultDto>
    {
        private readonly IApplicationDbContext _context;
        private readonly IJwtTokenService _jwtTokenService;
        private readonly IDateTime _dateTime;

        public RefreshTokenCommandHandler(
            IApplicationDbContext context,
            IJwtTokenService jwtTokenService,
            IDateTime dateTime)
        {
            _context = context;
            _jwtTokenService = jwtTokenService;
            _dateTime = dateTime;
        }

        public async Task<AuthResultDto> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
        {
            var existingToken = await _context.RefreshTokens
                .Include(rt => rt.User)
                    .ThenInclude(u => u.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

            if (existingToken is null)
                throw new NotFoundException("Refresh token", request.RefreshToken);

            if (existingToken.RevokedAt is not null)
            {
                // Token reuse detected — revoke all active tokens for this user as a precaution.
                var userTokens = await _context.RefreshTokens
                    .Where(rt => rt.UserId == existingToken.UserId && rt.RevokedAt == null)
                    .ToListAsync(cancellationToken);

                foreach (var t in userTokens) t.RevokedAt = _dateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);

                throw new ForbiddenAccessException();
            }

            if (!existingToken.IsActive)
                throw new ForbiddenAccessException();

            var newRefreshTokenValue = _jwtTokenService.GenerateRefreshToken();

            existingToken.RevokedAt = _dateTime.UtcNow;
            existingToken.ReplacedByToken = newRefreshTokenValue;

            var newRefreshToken = new RefreshToken
            {
                UserId = existingToken.UserId,
                Token = newRefreshTokenValue,
                CreatedAt = _dateTime.UtcNow,
                ExpiresAt = _dateTime.UtcNow.AddDays(7)
            };
            _context.RefreshTokens.Add(newRefreshToken);

            var permissions = existingToken.User.Role.RolePermissions
                .Select(rp => rp.Permission.Code)
                .ToList();

            var accessToken = _jwtTokenService.GenerateAccessToken(existingToken.User, permissions);

            await _context.SaveChangesAsync(cancellationToken);

            return new AuthResultDto
            {
                UserId = existingToken.User.Id,
                FullName = existingToken.User.FullName,
                Email = existingToken.User.Email,
                Role = existingToken.User.Role.Name,
                AccessToken = accessToken,
                RefreshToken = newRefreshTokenValue,
                AccessTokenExpiresAt = _dateTime.UtcNow.AddMinutes(_jwtTokenService.AccessTokenExpiryMinutes)
            };
        }
    }
}
