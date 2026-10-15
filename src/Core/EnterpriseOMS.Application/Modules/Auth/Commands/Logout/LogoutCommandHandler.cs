using EnterpriseOMS.Application.Common.Interfaces;
using EnterpriseOMS.Application.Common.Models;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnterpriseOMS.Application.Modules.Auth.Commands.Logout
{
    /// <summary>
    /// Revokes ONE refresh token (the session being ended). Other sessions of the same user,
    /// for example on another device, stay signed in.
    /// Idempotent: an unknown or already-revoked token is treated as success, so retries are
    /// safe and the endpoint reveals nothing about which tokens exist.
    /// The refresh token itself is the credential, so this endpoint needs no access token.
    /// That matters when the access token has already expired at logout time.
    /// </summary>
    public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
    {
        private readonly IApplicationDbContext _context;
        private readonly IDateTime _dateTime;

        public LogoutCommandHandler(IApplicationDbContext context, IDateTime dateTime)
        {
            _context = context;
            _dateTime = dateTime;
        }

        public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
        {
            var token = await _context.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, cancellationToken);

            if (token is not null && token.RevokedAt is null)
            {
                token.RevokedAt = _dateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }
    }
}
