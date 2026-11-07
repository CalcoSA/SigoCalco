using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using SigoCalco.Authentication.Api.Controllers;
using SigoCalco.Authentication.Api.Extensions;
using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Application.Services;
using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Exceptions;
using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Domain.Responses;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace SigoCalco.Authentication.Test;

public class SessionApiTests
{
    private const string Sid = "11111111-1111-1111-1111-111111111111";
    private const string Secret = "test-only-key-with-at-least-sixty-four-ascii-characters-0123456789ABCDEF";
    private static readonly AuthenticationScheme Scheme = new("Bearer", "Bearer", typeof(JwtBearerHandler));

    private static ClaimsPrincipal ValidatedJwt()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = Secret, ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience", ["Jwt:ExpirationMinutes"] = "480"
        }).Build();
        var user = new UserDto { IdUser = 42, WordpressUserId = 123, UserLogin = "demo", UserName = "Demo", Email = "demo@example.com", IdRole = 7, NameRole = "Role" };
        var before = DateTime.UtcNow;
        var issued = new JwtTokenApplication(configuration).GenerateToken(user, Sid);
        var after = DateTime.UtcNow;
        var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(issued.Token,
            new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret)),
                ValidateIssuer = true, ValidIssuer = "test-issuer", ValidateAudience = true, ValidAudience = "test-audience",
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true, ClockSkew = TimeSpan.Zero
            }, out _);
        Assert.InRange(issued.ExpiresAt, before.AddHours(8), after.AddHours(8));
        Assert.Equal(Sid, principal.FindFirst("sid")?.Value);
        Assert.Equal("42", principal.FindFirst("sub")?.Value);
        Assert.Equal("42", principal.FindFirst("idUser")?.Value);
        Assert.Equal("123", principal.FindFirst("wordpressUserId")?.Value);
        Assert.Equal("demo", principal.FindFirst("userLogin")?.Value);
        Assert.Equal("Demo", principal.FindFirst("userName")?.Value);
        Assert.Equal("demo@example.com", principal.FindFirst("email")?.Value);
        Assert.Equal("demo@example.com", principal.GetEmail());
        Assert.Equal("7", principal.FindFirst("idRole")?.Value);
        Assert.Equal("Role", principal.FindFirst("nameRole")?.Value);
        return principal;
    }

    private static DefaultHttpContext Http() => new() { Response = { Body = new MemoryStream() } };
    private static SessionJwtBearerEvents Events(Mock<IAuthApplication> app)
        => new(app.Object, NullLogger<SessionJwtBearerEvents>.Instance);
    private static TokenValidatedContext Validation(HttpContext http)
        => new(http, Scheme, new JwtBearerOptions()) { Principal = ValidatedJwt() };

    private static async Task<JsonElement> Challenge(SessionJwtBearerEvents events, HttpContext http)
    {
        await events.Challenge(new JwtBearerChallengeContext(http, Scheme, new JwtBearerOptions(), new AuthenticationProperties()));
        http.Response.Body.Position = 0;
        using var json = await JsonDocument.ParseAsync(http.Response.Body);
        return json.RootElement.Clone();
    }

    [Fact]
    public async Task ValidJwtChecksItsSessionWithoutRecordingActivity()
    {
        var app = new Mock<IAuthApplication>();
        app.Setup(x => x.ValidateSession(Sid, 42)).ReturnsAsync(new AuthenticationSession());
        var context = Validation(Http());
        await Events(app).TokenValidated(context);
        Assert.Null(context.Result);
        app.Verify(x => x.ValidateSession(Sid, 42), Times.Once);
        app.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("SESSION_EXPIRED")]
    [InlineData("SESSION_REVOKED")]
    public async Task UnexpiredSignedJwtCannotBypassInactiveSession(string code)
    {
        var app = new Mock<IAuthApplication>();
        app.Setup(x => x.ValidateSession(Sid, 42)).ThrowsAsync(new SessionException(code, "Inactive session"));
        var http = Http();
        var context = Validation(http);
        var events = Events(app);
        await events.TokenValidated(context);
        Assert.NotNull(context.Result?.Failure);
        var body = await Challenge(events, http);
        Assert.Equal(401, http.Response.StatusCode);
        Assert.Equal(code, body.GetProperty("result").GetProperty("code").GetString());
        Assert.Equal("Bearer", http.Response.Headers.WWWAuthenticate.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MissingOrDuplicateSidRequiresFreshLogin(bool duplicate)
    {
        var app = new Mock<IAuthApplication>();
        var http = Http();
        var context = Validation(http);
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        if (duplicate)
            identity.AddClaim(new Claim("sid", Sid));
        else
            identity.RemoveClaim(identity.FindFirst("sid")!);
        var events = Events(app);
        await events.TokenValidated(context);
        var body = await Challenge(events, http);
        Assert.Equal(401, http.Response.StatusCode);
        Assert.Equal("SESSION_REQUIRED", body.GetProperty("result").GetProperty("code").GetString());
        app.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task MismatchedOwnerClaimsAreRejectedBeforeDatabaseLookup()
    {
        var app = new Mock<IAuthApplication>();
        var http = Http();
        var context = Validation(http);
        var identity = (ClaimsIdentity)context.Principal!.Identity!;
        identity.RemoveClaim(identity.FindFirst("sub")!);
        identity.AddClaim(new Claim("sub", "99"));
        var events = Events(app);
        await events.TokenValidated(context);
        var body = await Challenge(events, http);
        Assert.Equal("TOKEN_INVALID", body.GetProperty("result").GetProperty("code").GetString());
        app.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task DatabaseFailureDeniesAccessWith503()
    {
        var app = new Mock<IAuthApplication>();
        app.Setup(x => x.ValidateSession(Sid, 42)).ThrowsAsync(new InvalidOperationException("database failure"));
        var http = Http();
        var context = Validation(http);
        var events = Events(app);
        await events.TokenValidated(context);
        Assert.NotNull(context.Result?.Failure);
        var body = await Challenge(events, http);
        Assert.Equal(503, http.Response.StatusCode);
        Assert.Equal("SESSION_UNAVAILABLE", body.GetProperty("result").GetProperty("code").GetString());
        Assert.DoesNotContain("database failure", body.GetRawText());
    }

    [Theory]
    [InlineData(true, "TOKEN_EXPIRED")]
    [InlineData(false, "TOKEN_INVALID")]
    public async Task JwtFailureCodesRemainDistinct(bool expired, string code)
    {
        var http = Http();
        var events = Events(new Mock<IAuthApplication>());
        await events.AuthenticationFailed(new AuthenticationFailedContext(http, Scheme, new JwtBearerOptions())
        {
            Exception = expired ? new SecurityTokenExpiredException() : new SecurityTokenInvalidSignatureException()
        });
        var body = await Challenge(events, http);
        Assert.Equal(401, http.Response.StatusCode);
        Assert.Equal(code, body.GetProperty("result").GetProperty("code").GetString());
    }

    [Fact]
    public async Task AnonymousRefreshDoesNotAuthenticateAnExpiredAuthorizationHeader()
    {
        var http = Http();
        http.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(new AllowAnonymousAttribute()), "refresh"));
        var context = new MessageReceivedContext(http, Scheme, new JwtBearerOptions());
        await Events(new Mock<IAuthApplication>()).MessageReceived(context);
        Assert.True(context.Result?.None);
    }

    [Fact]
    public async Task ActivityThrottleReturnsAuthoritativeDeadlineAndRetryAfter()
    {
        var app = new Mock<IAuthApplication>();
        var expires = DateTime.UtcNow.AddMinutes(5);
        app.Setup(x => x.RegisterActivity(Sid, 42)).ThrowsAsync(new SessionException("ACTIVITY_THROTTLED", "Wait", expires, 7));
        var http = Http();
        http.User = ValidatedJwt();
        var controller = new AuthController(app.Object, Mock.Of<ILogApplication>(), NullLogger<AuthController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = http }
        };
        var result = Assert.IsType<ObjectResult>(await controller.Activity());
        Assert.Equal(429, result.StatusCode);
        Assert.Equal("7", http.Response.Headers.RetryAfter.ToString());
        var response = Assert.IsType<ResponseApi>(result.Value);
        var body = JsonSerializer.SerializeToElement(response.Result);
        Assert.Equal("ACTIVITY_THROTTLED", body.GetProperty("code").GetString());
        Assert.Equal(expires, body.GetProperty("sessionExpiresAt").GetDateTime());
    }
}
