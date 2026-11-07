using System.Security.Cryptography;
using System.Text;
using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Application.Services;
using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Exceptions;
using SigoCalco.Authentication.Domain.Interfaces;
using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Domain.Options;
using AutoMapper;
using Microsoft.Extensions.Options;
using Moq;

namespace SigoCalco.Authentication.Test;

public class AuthApplicationSessionTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    private sealed class Fixture
    {
        public Mock<IWordpressUserRepository> Wordpress { get; } = new();
        public Mock<ISSOSignatureApplication> Sso { get; } = new();
        public Mock<IMenuOptionApplication> Menus { get; } = new();
        public Mock<IJwtTokenApplication> Jwt { get; } = new();
        public Mock<IUserRepository> Users { get; } = new();
        public Mock<IMapper> Mapper { get; } = new();
        public Mock<ISessionRepository> Sessions { get; } = new();
        public Mock<TimeProvider> Clock { get; } = new();
        public UserDto UserDto { get; } = new() { IdUser = 42, WordpressUserId = 123, UserLogin = "demo", UserName = "Demo", StatusUser = true, IdRole = 7, NameRole = "Role", StatusRole = 1 };
        public List<AuthenticationSession> Created { get; } = new();
        public AuthApplication App { get; }

        public Fixture()
        {
            var user = new User { IdUser = 42, WordpressUserId = 123, UserLogin = "demo", UserName = "Demo", StatusUser = true,
                Email = "demo@example.com", Userroles = [new Userrole { IdRole = 7, SolutionCenterId = 101 }] };
            Wordpress.Setup(x => x.GetByLogin("demo")).ReturnsAsync(new WordpressUserDto { WordpressUserId = 123, WordpressUserLogin = "demo", WordpressUserPass = "stored-password-hash" });
            Wordpress.Setup(x => x.Verify("password", "stored-password-hash")).Returns(true);
            Users.Setup(x => x.GetUserByLogin("demo")).ReturnsAsync(user);
            Users.Setup(x => x.GetUserById(42)).ReturnsAsync(user);
            Mapper.Setup(x => x.Map<UserDto>(It.IsAny<object>())).Returns(UserDto);
            Menus.Setup(x => x.GetByRole(7)).ReturnsAsync(new[] { new MenuOptionDto() });
            Jwt.Setup(x => x.GenerateToken(It.IsAny<UserDto>(), It.IsAny<string>())).Returns(("signed-access-token", Now.AddMinutes(30)));
            Clock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(Now));
            Sessions.Setup(x => x.Create(It.IsAny<AuthenticationSession>())).Callback<AuthenticationSession>(Created.Add).Returns(Task.CompletedTask);
            App = new AuthApplication(Wordpress.Object, Sso.Object, Menus.Object, Jwt.Object, Users.Object, Mapper.Object,
                Sessions.Object, Options.Create(new SessionOptions { IdleTimeoutMinutes = 5 }), Clock.Object);
        }
    }

    [Fact]
    public async Task LoginCreatesSessionAndStoresOnlyHash()
    {
        var fixture = new Fixture();
        var result = await fixture.App.Login(new LoginDto { Username = "demo", Password = "password" });
        Assert.NotNull(result);
        var session = Assert.Single(fixture.Created);
        Assert.True(Guid.TryParse(session.IdSession, out _));
        Assert.Equal(42, session.IdUser);
        Assert.Equal(Now, session.CreatedAt);
        Assert.Equal(Now, session.LastActivityAt);
        Assert.Equal(Now.AddMinutes(5), session.ExpiresAt);
        Assert.Equal(session.ExpiresAt, result.SessionExpiresAt);
        Assert.Equal("signed-access-token", result.AccessToken);
        Assert.Equal(80, result.RefreshToken!.Length);
        Assert.StartsWith(session.IdSession + ".", result.RefreshToken);
        Assert.Equal(Hash(result.RefreshToken), session.RefreshTokenHash);
        fixture.Jwt.Verify(x => x.GenerateToken(fixture.UserDto, session.IdSession), Times.Once);
    }

    [Fact]
    public async Task RepeatedLoginsCreateIndependentSessionsAndSecrets()
    {
        var fixture = new Fixture();
        var first = await fixture.App.Login(new LoginDto { Username = "demo", Password = "password" });
        var second = await fixture.App.Login(new LoginDto { Username = "demo", Password = "password" });
        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.Equal(2, fixture.Created.Count);
        Assert.NotEqual(fixture.Created[0].IdSession, fixture.Created[1].IdSession);
        Assert.NotEqual(first.RefreshToken, second.RefreshToken);
        Assert.NotEqual(fixture.Created[0].RefreshTokenHash, fixture.Created[1].RefreshTokenHash);
    }

    [Fact]
    public async Task SsoCreatesServerSideSession()
    {
        var fixture = new Fixture();
        fixture.Sso.Setup(x => x.IsValid("demo", 123, "signature")).Returns(true);
        var result = await fixture.App.IntranetAccess(new IntranetAccessDto { UserLogin = "demo", Ts = 123, Sig = "signature" });
        Assert.NotNull(result);
        var session = Assert.Single(fixture.Created);
        Assert.Equal(42, session.IdUser);
        Assert.Equal(Now.AddMinutes(5), result.SessionExpiresAt);
        Assert.Equal(Hash(result.RefreshToken!), session.RefreshTokenHash);
        fixture.Jwt.Verify(x => x.GenerateToken(fixture.UserDto, session.IdSession), Times.Once);
    }

    [Fact]
    public async Task InvalidCredentialsOrSignatureCannotCreateSession()
    {
        var fixture = new Fixture();
        Assert.Null(await fixture.App.Login(new LoginDto { Username = "demo", Password = "invalid" }));
        Assert.Null(await fixture.App.IntranetAccess(new IntranetAccessDto { UserLogin = "demo", Ts = 123, Sig = "invalid" }));
        Assert.Empty(fixture.Created);
        fixture.Jwt.Verify(x => x.GenerateToken(It.IsAny<UserDto>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RefreshRotatesSecretPreservingSessionDeadlineAndSid()
    {
        var fixture = new Fixture();
        const string sid = "11111111-1111-1111-1111-111111111111";
        var token = sid + "." + new string('A', 43);
        var session = new AuthenticationSession { IdSession = sid, IdUser = 42, CreatedAt = Now.AddMinutes(-3), LastActivityAt = Now.AddMinutes(-3), ExpiresAt = Now.AddMinutes(2), RefreshTokenHash = Hash(token) };
        string? replacementHash = null;
        fixture.Sessions.Setup(x => x.RotateRefreshToken(sid, Hash(token), It.IsAny<string>()))
            .Callback<string, string, string>((_, _, hash) => replacementHash = hash).ReturnsAsync(session);
        var result = await fixture.App.Refresh(token);
        Assert.Equal(session.ExpiresAt, result.SessionExpiresAt);
        Assert.Equal(Now.AddMinutes(-3), session.LastActivityAt);
        Assert.NotEqual(token, result.RefreshToken);
        Assert.StartsWith(sid + ".", result.RefreshToken!);
        Assert.Equal(Hash(result.RefreshToken!), replacementHash);
        fixture.Jwt.Verify(x => x.GenerateToken(fixture.UserDto, sid), Times.Once);
        fixture.Sessions.Verify(x => x.RegisterActivity(It.IsAny<string>(), It.IsAny<int>()), Times.Never);
        fixture.Sessions.Verify(x => x.Create(It.IsAny<AuthenticationSession>()), Times.Never);
    }

    [Fact]
    public async Task ExpiredRefreshNeverIssuesJwt()
    {
        var fixture = new Fixture();
        var token = Guid.NewGuid().ToString("D") + "." + new string('A', 43);
        fixture.Sessions.Setup(x => x.RotateRefreshToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new SessionException("SESSION_EXPIRED", "Expired"));
        await Assert.ThrowsAsync<SessionException>(() => fixture.App.Refresh(token));
        fixture.Jwt.Verify(x => x.GenerateToken(It.IsAny<UserDto>(), It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("expired.jwt.is.not.a.refresh.token")]
    public async Task JwtOrMalformedRefreshIsInsufficient(string token)
    {
        var fixture = new Fixture();
        Assert.Equal("REFRESH_INVALID", (await Assert.ThrowsAsync<SessionException>(() => fixture.App.Refresh(token))).Code);
        fixture.Sessions.Verify(x => x.RotateRefreshToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task LogoutRevokesOnlyRequestedSession()
    {
        var fixture = new Fixture();
        fixture.Sessions.Setup(x => x.Revoke("sid", 42)).Returns(Task.CompletedTask);
        await fixture.App.Logout("sid", 42);
        fixture.Sessions.Verify(x => x.Revoke("sid", 42), Times.Once);
        fixture.Sessions.VerifyNoOtherCalls();
    }
}
