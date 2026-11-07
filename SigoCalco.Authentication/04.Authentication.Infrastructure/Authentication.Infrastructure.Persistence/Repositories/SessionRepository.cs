using SigoCalco.Authentication.Domain.Exceptions;
using SigoCalco.Authentication.Domain.Interfaces;
using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Domain.Options;
using SigoCalco.Authentication.Infrastructure.Persistence.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace SigoCalco.Authentication.Infrastructure.Persistence.Repositories
{
    public class SessionRepository : Repository<AuthenticationSession>, ISessionRepository
    {
        private readonly SigoCalcoContext _context;
        private readonly TimeProvider _timeProvider;
        private readonly SessionOptions _options;
        private readonly ILogRepository _logRepository;

        public SessionRepository(SigoCalcoContext context, TimeProvider timeProvider,
            IOptions<SessionOptions> options, ILogRepository logRepository) : base(context)
        {
            _context = context;
            _timeProvider = timeProvider;
            _options = options.Value;
            _logRepository = logRepository;
        }

        public Task Create(AuthenticationSession session) => Add(session);

        public Task<AuthenticationSession> Validate(string sessionId, int idUser)
            => WithLockedSession(sessionId, idUser, null);

        public Task<AuthenticationSession> RegisterActivity(string sessionId, int idUser)
            => WithLockedSession(sessionId, idUser, (session, now) =>
                session.RegisterActivity(now, TimeSpan.FromMinutes(_options.IdleTimeoutMinutes)));

        public Task<AuthenticationSession> RotateRefreshToken(string sessionId, string currentHash, string newHash)
            => WithLockedSession(sessionId, null, (session, now) =>
                session.RotateRefreshToken(now, currentHash, newHash));

        public async Task Revoke(string sessionId, int idUser)
            => await WithLockedSession(sessionId, idUser, (session, now) => session.Revoke(now), "CerrarSesion");

        private async Task<AuthenticationSession> WithLockedSession(string sessionId, int? idUser,
            Action<AuthenticationSession, DateTime>? update, string? action = null)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            var sessions = await _context.AuthenticationSessions
                .FromSqlInterpolated($"SELECT * FROM sigocalco_authentication_sessions WHERE id_session = {sessionId} FOR UPDATE")
                .AsNoTracking()
                .ToListAsync();
            var session = sessions.SingleOrDefault();
            if (session is null || (idUser.HasValue && session.IdUser != idUser.Value))
                throw new SessionException(idUser.HasValue ? "SESSION_REQUIRED" : "REFRESH_INVALID",
                    "No se encontró una sesión válida. Inicie sesión nuevamente.");

            var tracked = _context.AuthenticationSessions.Local.FirstOrDefault(x => x.IdSession == sessionId);
            if (tracked is not null)
                _context.Entry(tracked).State = EntityState.Detached;
            _context.Attach(session);

            try
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                try
                {
                    session.EnsureActive(now);
                }
                catch (SessionException ex) when (ex.Code == "SESSION_EXPIRED")
                {
                    if (session.RefreshTokenHash is not null)
                    {
                        session.RefreshTokenHash = null;
                        await _context.SaveChangesAsync();
                        await WriteLog(session, "SesionVencida", "La sesión venció por inactividad.");
                    }
                    await transaction.CommitAsync();
                    throw;
                }

                update?.Invoke(session, now);
                if (update is not null)
                    await _context.SaveChangesAsync();
                if (action is not null)
                    await WriteLog(session, action, "Se cerró la sesión correctamente.");
                await transaction.CommitAsync();
                return session;
            }
            finally
            {
                _context.Entry(session).State = EntityState.Detached;
            }
        }

        private async Task WriteLog(AuthenticationSession session, string action, string description)
        {
            var userName = await _context.Users.Where(user => user.IdUser == session.IdUser)
                .Select(user => user.UserLogin).SingleAsync();
            await _logRepository.CreateLog(new AuthenticationLog
            {
                Action = action,
                Module = "Autenticacion",
                Description = description,
                UserName = userName
            });
        }
    }
}
