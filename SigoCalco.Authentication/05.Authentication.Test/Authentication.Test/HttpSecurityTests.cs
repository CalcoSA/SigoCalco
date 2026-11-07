using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Moq;
using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Interfaces;
using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Infrastructure.Persistence.Data;

namespace SigoCalco.Authentication.Test;

public class HttpSecurityTests
{
    private sealed class Clock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan elapsed) => _now += elapsed;
    }

    private sealed class TestContext(DbContextOptions<SigoCalcoContext> options) : SigoCalcoContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<SolutionCenter>().Property(x => x.SolutionCenterId).HasColumnType("INTEGER");
            builder.Entity<AuthenticationLog>().Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }

    private sealed class Commands : DbCommandInterceptor
    {
        public bool FailAudit { get; set; }
        public int Reads { get; set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Reads++;
            if (command.CommandText.Contains(" FOR UPDATE") || command.CommandText.Contains(" LOCK IN SHARE MODE"))
            {
                Assert.NotNull(eventData.Context!.Database.CurrentTransaction);
                // SQLite exercises persistence and rollback, not MySQL lock semantics.
                command.CommandText = command.CommandText.Replace(" FOR UPDATE", "")
                    .Replace(" LOCK IN SHARE MODE", "");
            }
            if (FailAudit && command.CommandText.Contains("INSERT INTO \"sigocalco_authentication_log\""))
                throw new InvalidOperationException("Synthetic audit persistence failure.");
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class Api : WebApplicationFactory<Program>
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        public Clock Clock { get; } = new();
        public Commands Commands { get; } = new();
        public string Secret { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        public const string Issuer = "authentication-test-issuer";
        public const string Audience = "authentication-test-audience";
        public HttpClient Client { get; private set; } = null!;

        private Dictionary<string, string?> Settings => new()
        {
            ["ConnectionStrings:SigoCalcoDatabase"] = "Server=never-connect.invalid;Database=bdcalco_sigocalco;",
            ["ConnectionStrings:WordPressDatabase"] = "Server=never-connect.invalid;Database=unused;",
            ["Jwt:Secret"] = Secret, ["Jwt:Issuer"] = Issuer, ["Jwt:Audience"] = Audience,
            ["Jwt:ExpirationMinutes"] = "30", ["Session:IdleTimeoutMinutes"] = "5",
            ["Cors:AllowedOrigins:0"] = "https://frontend.invalid",
            ["Logging:LogLevel:Default"] = "Error"
        };

        protected override IHost CreateHost(IHostBuilder builder)
        {
            // Minimal hosting reads these settings before ConfigureWebHost callbacks.
            // DeferredHostBuilder passes host settings as entry-point arguments.
            builder.ConfigureHostConfiguration(config => config.AddInMemoryCollection(Settings));
            return base.CreateHost(builder);
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                // Never use the developer's real connections or signing keys.
                config.Sources.Clear();
                config.AddInMemoryCollection(Settings);
            });
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<SigoCalcoContext>();
                services.RemoveAll<DbContextOptions<SigoCalcoContext>>();
                services.RemoveAll<IDbContextOptionsConfiguration<SigoCalcoContext>>();
                services.AddScoped<SigoCalcoContext>(_ => new TestContext(
                    new DbContextOptionsBuilder<SigoCalcoContext>().UseSqlite(_connection)
                        .AddInterceptors(Commands).Options));
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                var wordpress = new Mock<IWordpressUserRepository>(MockBehavior.Strict);
                wordpress.Setup(x => x.GetByLogin("test.user")).ReturnsAsync(new WordpressUserDto
                {
                    WordpressUserId = 123, WordpressUserLogin = "test.user",
                    WordpressUserPass = "synthetic-password-hash", WordpressUserEmail = "wordpress@example.com"
                });
                wordpress.Setup(x => x.Verify("synthetic-password", "synthetic-password-hash")).Returns(true);
                services.RemoveAll<IWordpressUserRepository>();
                services.AddSingleton(wordpress.Object);
            });
        }

        public async Task Initialize()
        {
            await _connection.OpenAsync();
            _connection.CreateCollation("ascii_bin", string.CompareOrdinal);
            Client = CreateClient(new WebApplicationFactoryClientOptions
            { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<SigoCalcoContext>();
            Assert.Equal("Microsoft.EntityFrameworkCore.Sqlite", db.Database.ProviderName);
            await db.Database.EnsureCreatedAsync();
            db.Roles.Add(new Role { IdRole = 7, NameRole = "Administrador", StatusRole = 1 });
            db.SolutionCenters.Add(new SolutionCenter
            { SolutionCenterId = 101, SolutionCenterCode = "TEST", SolutionCenterName = "Test only" });
            db.Menuoptions.Add(new Menuoption { IdMenuOption = 5, NameMenuOption = "Test", StatusMenuOption = 1 });
            db.Rolemenuoptions.Add(new Rolemenuoption { IdRole = 7, IdMenuOption = 5 });
            db.Users.Add(new User
            {
                IdUser = 42, WordpressUserId = 123, UserLogin = "test.user", UserName = "Test user",
                Email = "local@example.com", StatusUser = true,
                Userroles = [new Userrole { IdRole = 7, SolutionCenterId = 101 }]
            });
            await db.SaveChangesAsync();
            Commands.Reads = 0;
        }

        public async Task<AuthUserDto> Login()
            => await ReadAuth(await Client.PostAsJsonAsync("/api/auth/login", new
            { username = "test.user", password = "synthetic-password" }));

        public async Task<AuthenticationSession> Session(string sid)
        {
            using var scope = Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<SigoCalcoContext>()
                .AuthenticationSessions.AsNoTracking().SingleAsync(x => x.IdSession == sid);
        }

        public async Task<int> AuditCount(string action)
        {
            using var scope = Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<SigoCalcoContext>()
                .AuthenticationLogs.CountAsync(x => x.Action == action && x.UserName == "test.user");
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    public static TheoryData<string, string> ProtectedRoutes => new()
    {
        { "GET", "/api/user" }, { "GET", "/api/user/42" }, { "POST", "/api/user" },
        { "PUT", "/api/user" }, { "DELETE", "/api/user/42" },
        { "GET", "/api/role" }, { "GET", "/api/role/7" }, { "POST", "/api/role" },
        { "PUT", "/api/role" }, { "DELETE", "/api/role/7" },
        { "GET", "/api/menuoption" }, { "GET", "/api/menuoption/role/7" },
        { "GET", "/api/wordpressuser/test.user" }, { "GET", "/api/Authentication/SolutionCenters" },
        { "GET", "/api/log" }, { "POST", "/api/auth/activity" }, { "POST", "/api/auth/logout" }
    };

    [Theory]
    [MemberData(nameof(ProtectedRoutes))]
    public async Task EveryProtectedEndpointRejectsMissingAndMalformedAuthorization(string method, string path)
    {
        await using var api = new Api();
        await api.Initialize();
        foreach (var token in new string?[] { null, "not-a-jwt" })
        {
            using var request = new HttpRequestMessage(new HttpMethod(method), path)
            { Content = JsonContent.Create(new { }) };
            if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var response = await api.Client.SendAsync(request);
            await AssertFailure(response, token is null ? "AUTH_REQUIRED" : "TOKEN_INVALID");
        }
        Assert.Equal(0, api.Commands.Reads);
    }

    [Fact]
    public async Task EndpointMetadataContainsOnlyTheThreeExpectedAnonymousActions()
    {
        await using var api = new Api();
        await api.Initialize();
        var routes = api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>().ToList();
        var anonymous = routes.Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is not null)
            .Select(e => e.RoutePattern.RawText!.ToLowerInvariant()).Order().ToArray();
        Assert.Equal(new[] { "api/auth/intranet-access", "api/auth/login", "api/auth/refresh" }, anonymous);
        var protectedRoutes = routes.Where(e => e.Metadata.GetMetadata<IAllowAnonymous>() is null).ToList();
        Assert.Equal(17, protectedRoutes.Count);
        Assert.All(protectedRoutes, endpoint => Assert.NotEmpty(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>()));
    }

    [Fact]
    public async Task RefreshRotatesPersistedHashWithoutActivityAndRejectsReplayThenLogoutRevokesBothTokens()
    {
        await using var api = new Api();
        await api.Initialize();
        var login = await api.Login();
        var sid = Sid(login);
        var before = await api.Session(sid);
        Assert.Equal(Hash(login.RefreshToken!), before.RefreshTokenHash);
        api.Clock.Advance(TimeSpan.FromMinutes(1));
        var refreshed = await ReadAuth(await api.Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = login.RefreshToken }));
        Assert.Equal(sid, Sid(refreshed));
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        var after = await api.Session(sid);
        Assert.Equal(Hash(refreshed.RefreshToken!), after.RefreshTokenHash);
        Assert.Equal(before.ExpiresAt, after.ExpiresAt);
        Assert.Equal(before.LastActivityAt, after.LastActivityAt);
        await AssertFailure(await api.Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = login.RefreshToken }), "REFRESH_INVALID");
        Assert.Equal(after.RefreshTokenHash, (await api.Session(sid)).RefreshTokenHash);

        api.Client.DefaultRequestHeaders.Authorization = new("Bearer", refreshed.AccessToken);
        (await api.Client.PostAsJsonAsync("/api/auth/logout", new { })).EnsureSuccessStatusCode();
        var revoked = await api.Session(sid);
        Assert.NotNull(revoked.RevokedAt);
        Assert.Null(revoked.RefreshTokenHash);
        Assert.Equal(1, await api.AuditCount("CerrarSesion"));
        await AssertFailure(await api.Client.GetAsync("/api/user"), "SESSION_REVOKED");
        await AssertFailure(await api.Client.PostAsJsonAsync("/api/auth/activity", new { }), "SESSION_REVOKED");
        await AssertFailure(await api.Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = refreshed.RefreshToken }), "SESSION_REVOKED");
        await AssertFailure(await api.Client.PostAsJsonAsync("/api/auth/logout", new { }), "SESSION_REVOKED");
        api.Client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        await AssertFailure(await api.Client.GetAsync("/api/user"), "SESSION_REVOKED");
        Assert.Equal(1, await api.AuditCount("CerrarSesion"));
    }

    [Fact]
    public async Task ExpirationClearsRefreshOnceAuditsOnceAndCannotBeRevived()
    {
        await using var api = new Api();
        await api.Initialize();
        var login = await api.Login();
        api.Client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        api.Clock.Advance(TimeSpan.FromMinutes(5));
        for (var i = 0; i < 2; i++)
            await AssertFailure(await api.Client.GetAsync("/api/user"), "SESSION_EXPIRED");
        await AssertFailure(await api.Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = login.RefreshToken }), "SESSION_EXPIRED");
        await AssertFailure(await api.Client.PostAsJsonAsync("/api/auth/activity", new { }), "SESSION_EXPIRED");
        Assert.Null((await api.Session(Sid(login))).RefreshTokenHash);
        Assert.Equal(1, await api.AuditCount("SesionVencida"));
        // Even a later clock correction cannot revive an already persisted expiry.
        api.Clock.Advance(TimeSpan.FromMinutes(-1));
        await AssertFailure(await api.Client.GetAsync("/api/user"), "SESSION_EXPIRED");
    }

    [Fact]
    public async Task LogoutDoesNotRevokeAnIndependentSessionOfTheSameUser()
    {
        await using var api = new Api();
        await api.Initialize();
        var first = await api.Login();
        var second = await api.Login();
        api.Client.DefaultRequestHeaders.Authorization = new("Bearer", first.AccessToken);
        (await api.Client.PostAsJsonAsync("/api/auth/logout", new { })).EnsureSuccessStatusCode();
        api.Client.DefaultRequestHeaders.Authorization = new("Bearer", second.AccessToken);
        (await api.Client.GetAsync("/api/user")).EnsureSuccessStatusCode();
        Assert.Null((await api.Session(Sid(second))).RevokedAt);
    }

    [Fact]
    public async Task FailedLogoutAuditRollsBackRevocationAndDoesNotReportSuccess()
    {
        await using var api = new Api();
        await api.Initialize();
        var login = await api.Login();
        api.Client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        api.Commands.FailAudit = true;
        Assert.Equal(HttpStatusCode.InternalServerError,
            (await api.Client.PostAsJsonAsync("/api/auth/logout", new { })).StatusCode);
        var session = await api.Session(Sid(login));
        Assert.Null(session.RevokedAt);
        Assert.Equal(Hash(login.RefreshToken!), session.RefreshTokenHash);
        api.Commands.FailAudit = false;
        (await api.Client.PostAsJsonAsync("/api/auth/logout", new { })).EnsureSuccessStatusCode();
        Assert.Equal(1, await api.AuditCount("CerrarSesion"));
    }

    [Theory]
    [InlineData("signature", "TOKEN_INVALID")]
    [InlineData("issuer", "TOKEN_INVALID")]
    [InlineData("audience", "TOKEN_INVALID")]
    [InlineData("expiry", "TOKEN_EXPIRED")]
    [InlineData("owner", "SESSION_REQUIRED")]
    public async Task JwtValidationRejectsTamperingWrongRecipientsExpiryAndWrongSessionOwner(string invalid, string code)
    {
        await using var api = new Api();
        await api.Initialize();
        var login = await api.Login();
        var token = BuildToken(api, login, invalid);
        api.Client.DefaultRequestHeaders.Authorization = new("Bearer", token);
        await AssertFailure(await api.Client.GetAsync("/api/user"), code);
    }

    [Fact]
    public async Task ValidRefreshWorksEvenWithExpiredAccessTokenHeaderAndKeepsTheLocalEmailClaim()
    {
        await using var api = new Api();
        await api.Initialize();
        var login = await api.Login();
        api.Client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
        var update = await api.Client.PutAsJsonAsync("/api/user", new { idUser = 42, email = "updated@example.com" });
        update.EnsureSuccessStatusCode();
        api.Client.DefaultRequestHeaders.Authorization = new("Bearer", BuildToken(api, login, "expiry"));
        var refreshed = await ReadAuth(await api.Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = login.RefreshToken }));
        Assert.Equal("updated@example.com", refreshed.User!.Email);
        var principal = new JwtSecurityTokenHandler { MapInboundClaims = false }.ValidateToken(refreshed.AccessToken,
            new TokenValidationParameters
            {
                ValidIssuer = Api.Issuer, ValidAudience = Api.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(api.Secret)),
                ValidateIssuerSigningKey = true, ValidateLifetime = true, ClockSkew = TimeSpan.Zero
            }, out _);
        Assert.Equal("updated@example.com", principal.FindFirst("email")!.Value);
        Assert.Equal("42", principal.FindFirst("idUser")!.Value);
        Assert.Equal(Sid(login), principal.FindFirst("sid")!.Value);
    }

    private static string BuildToken(Api api, AuthUserDto login, string invalid)
    {
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken).Claims
            .Where(c => c.Type is not "exp" and not "iss" and not "aud").ToList();
        if (invalid == "owner")
        {
            claims.RemoveAll(c => c.Type is "sub" or "idUser");
            claims.AddRange([new Claim("sub", "999"), new Claim("idUser", "999")]);
        }
        var token = new JwtSecurityToken(invalid == "issuer" ? "wrong" : Api.Issuer,
            invalid == "audience" ? "wrong" : Api.Audience, claims,
            expires: DateTime.UtcNow.AddMinutes(invalid == "expiry" ? -1 : 30),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                invalid == "signature" ? new string('Z', 64) : api.Secret)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static async Task<AuthUserDto> ReadAuth(HttpResponseMessage response)
    {
        response.EnsureSuccessStatusCode();
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(envelope.GetProperty("isSuccess").GetBoolean());
        return envelope.GetProperty("result").Deserialize<AuthUserDto>(new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    private static async Task AssertFailure(HttpResponseMessage response, string code)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(response.Headers.WwwAuthenticate, header => header.Scheme == "Bearer");
        var envelope = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(envelope.GetProperty("isSuccess").GetBoolean());
        Assert.Equal(code, envelope.GetProperty("result").GetProperty("code").GetString());
    }

    private static string Sid(AuthUserDto result) => new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken)
        .Claims.Single(c => c.Type == "sid").Value;
    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
