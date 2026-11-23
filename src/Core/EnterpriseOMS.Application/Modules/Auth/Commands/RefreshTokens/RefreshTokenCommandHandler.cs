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
    /// <summary>
    /// Implements refresh-token ROTATION: every use of a refresh token issues a brand new one and
    /// revokes the old, linking them via ReplacedByToken. This lets us detect token theft: if a
    /// revoked token is ever reused, we know it was stolen (a real client would not reuse it) and
    /// can revoke the entire chain.
    /// Like login, this runs before the caller's organization is known, so it ignores tenant filters.
    /// </summary>
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
                .IgnoreQueryFilters()
                .Include(rt => rt.User)
                    .ThenInclude(u => u.Organization)
                .Include(rt => rt.User)
                    .ThenInclude(u => u.Role)
                        .ThenInclude(r => r.RolePermissions)
                            .ThenInclude(rp => rp.Permission)
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

            if (existingToken is null)
                throw new NotFoundException("Refresh token", request.RefreshToken);

            if (existingToken.RevokedAt is not null)
            {
                // Token reuse detected: revoke all active tokens for this user as a precaution.
                var userTokens = await _context.RefreshTokens
                    .IgnoreQueryFilters()
                    .Where(rt => rt.UserId == existingToken.UserId && rt.RevokedAt == null)
                    .ToListAsync(cancellationToken);

                foreach (var t in userTokens) t.RevokedAt = _dateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);

                throw new ForbiddenAccessException();
            }

            if (!existingToken.IsActive)
                throw new ForbiddenAccessException();

            // A deactivated user or organization must not be able to keep a session alive.
            if (!existingToken.User.IsActive || !existingToken.User.Organization.IsActive)
                throw new ForbiddenAccessException();

            var newRefreshTokenValue = _jwtTokenService.GenerateRefreshToken();

            existingToken.RevokedAt = _dateTime.UtcNow;
            existingToken.ReplacedByToken = newRefreshTokenValue;

            _context.RefreshTokens.Add(new RefreshToken
            {
                UserId = existingToken.UserId,
                Token = newRefreshTokenValue,
                CreatedAt = _dateTime.UtcNow,
                ExpiresAt = _dateTime.UtcNow.AddDays(7)
            });

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
