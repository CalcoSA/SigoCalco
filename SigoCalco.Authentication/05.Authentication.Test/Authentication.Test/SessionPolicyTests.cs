using SigoCalco.Authentication.Domain.Exceptions;
using SigoCalco.Authentication.Domain.Models;

namespace SigoCalco.Authentication.Test;

public class SessionPolicyTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly TimeSpan Idle = TimeSpan.FromMinutes(5);
    private static readonly string OldHash = new('A', 64);
    private static readonly string NewHash = new('B', 64);

    private static AuthenticationSession Session() => new()
    {
        IdSession = Guid.NewGuid().ToString("D"), IdUser = 42, CreatedAt = Now,
        LastActivityAt = Now, ExpiresAt = Now.Add(Idle), RefreshTokenHash = OldHash
    };

    [Fact]
    public void ValidationDoesNotExtendDeadline()
    {
        var session = Session();
        session.EnsureActive(Now.AddMinutes(4));
        Assert.Equal(Now, session.LastActivityAt);
        Assert.Equal(Now.Add(Idle), session.ExpiresAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ExpirationIsInclusive(int extraSeconds)
    {
        var session = Session();
        Assert.Equal("SESSION_EXPIRED", Assert.Throws<SessionException>(() =>
            session.EnsureActive(session.ExpiresAt.AddSeconds(extraSeconds))).Code);
    }

    [Fact]
    public void ActivityAtFifteenSecondsExtendsDeadline()
    {
        var session = Session();
        var at = Now.AddSeconds(15);
        session.RegisterActivity(at, Idle);
        Assert.Equal(at, session.LastActivityAt);
        Assert.Equal(at.Add(Idle), session.ExpiresAt);
        Assert.Equal(Now, session.CreatedAt);
    }

    [Fact]
    public void FrequentActivityIsThrottledWithoutChangingDeadline()
    {
        var session = Session();
        var error = Assert.Throws<SessionException>(() => session.RegisterActivity(Now.AddMilliseconds(14100), Idle));
        Assert.Equal("ACTIVITY_THROTTLED", error.Code);
        Assert.Equal(1, error.RetryAfterSeconds);
        Assert.Equal(session.ExpiresAt, error.ExpiresAt);
        Assert.Equal(Now, session.LastActivityAt);
        Assert.Equal(Now.Add(Idle), session.ExpiresAt);
    }

    [Fact]
    public void ActivityJustBeforeExpirationExtendsDeadline()
    {
        var session = Session();
        var at = session.ExpiresAt.AddTicks(-1);
        session.RegisterActivity(at, Idle);
        Assert.Equal(at.Add(Idle), session.ExpiresAt);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void ActivityAtOrAfterExpirationCannotRevive(int extraSeconds)
    {
        var session = Session();
        Assert.Equal("SESSION_EXPIRED", Assert.Throws<SessionException>(() =>
            session.RegisterActivity(session.ExpiresAt.AddSeconds(extraSeconds), Idle)).Code);
        Assert.Equal(Now, session.LastActivityAt);
        Assert.Equal(Now.Add(Idle), session.ExpiresAt);
    }

    [Fact]
    public void ActiveSessionContinuesForEightHoursWithoutAnAbsoluteLimit()
    {
        var session = Session();
        for (var minute = 4; minute <= 480; minute += 4)
            session.RegisterActivity(Now.AddMinutes(minute), Idle);
        session.EnsureActive(Now.AddHours(8));
        Assert.Equal(Now.AddHours(8).Add(Idle), session.ExpiresAt);
        Assert.Equal(Now, session.CreatedAt);
    }

    [Fact]
    public void RefreshRotatesWithoutExtendingDeadline()
    {
        var session = Session();
        session.RotateRefreshToken(Now.AddMinutes(2), OldHash, NewHash);
        Assert.Equal(NewHash, session.RefreshTokenHash);
        Assert.Equal(Now, session.LastActivityAt);
        Assert.Equal(Now.Add(Idle), session.ExpiresAt);
    }

    [Fact]
    public void PreviousRefreshTokenCannotBeReplayed()
    {
        var session = Session();
        session.RotateRefreshToken(Now.AddMinutes(1), OldHash, NewHash);
        Assert.Equal("REFRESH_INVALID", Assert.Throws<SessionException>(() =>
            session.RotateRefreshToken(Now.AddMinutes(2), OldHash, OldHash)).Code);
        Assert.Equal(NewHash, session.RefreshTokenHash);
    }

    [Fact]
    public void RefreshAtExpirationCannotRevive()
    {
        var session = Session();
        Assert.Equal("SESSION_EXPIRED", Assert.Throws<SessionException>(() =>
            session.RotateRefreshToken(session.ExpiresAt, OldHash, NewHash)).Code);
    }

    [Fact]
    public void LogoutRevokesAndClearsRefreshToken()
    {
        var session = Session();
        session.Revoke(Now.AddMinutes(1));
        Assert.Equal(Now.AddMinutes(1), session.RevokedAt);
        Assert.Null(session.RefreshTokenHash);
        Assert.Equal("SESSION_REVOKED", Assert.Throws<SessionException>(() => session.EnsureActive(Now.AddMinutes(2))).Code);
        Assert.Equal("SESSION_REVOKED", Assert.Throws<SessionException>(() => session.RegisterActivity(Now.AddMinutes(2), Idle)).Code);
        Assert.Equal("SESSION_REVOKED", Assert.Throws<SessionException>(() => session.RotateRefreshToken(Now.AddMinutes(2), OldHash, NewHash)).Code);
    }

    [Fact]
    public void SessionsOfTheSameUserRemainIndependent()
    {
        var first = Session();
        var second = Session();
        first.RegisterActivity(Now.AddMinutes(4), Idle);
        second.Revoke(Now.AddMinutes(4));
        first.EnsureActive(Now.AddMinutes(6));
        Assert.NotEqual(first.IdSession, second.IdSession);
        Assert.Null(first.RevokedAt);
        Assert.Equal(OldHash, first.RefreshTokenHash);
        Assert.Equal(Now.AddMinutes(5), second.ExpiresAt);
    }

    [Fact]
    public void PersistedExpirationCannotBeReopenedByAnEarlierClock()
    {
        var session = Session();
        session.RefreshTokenHash = null;
        var earlierTime = Now.AddMinutes(4);
        Assert.Equal("SESSION_EXPIRED", Assert.Throws<SessionException>(() => session.EnsureActive(earlierTime)).Code);
        Assert.Equal("SESSION_EXPIRED", Assert.Throws<SessionException>(() => session.RegisterActivity(earlierTime, Idle)).Code);
        Assert.Equal("SESSION_EXPIRED", Assert.Throws<SessionException>(() => session.RotateRefreshToken(earlierTime, OldHash, NewHash)).Code);
    }
}
