using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Application.Services;
using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Exceptions;
using SigoCalco.Authentication.Domain.Interfaces;
using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Domain.Options;
using SigoCalco.Authentication.Domain.Responses;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;

namespace SigoCalco.Authentication.Test;

public partial class PointOfSaleAssignmentTests
{
    [Fact]
    public async Task EmailOnlyApplicationUpdateHandlesAnAlreadyTrackedUserAndAssignment()
    {
        await using var f = new Fixture();
        await f.Initialize();
        var user = await f.Create(OtherRole);
        await f.Context.Users.Include(x => x.Userroles).SingleAsync();
        var app = new UserApplication(f.Users, f.Mapper, f.Roles, f.Wordpress.Object);
        var result = await app.Update(new UpdateUserDto { IdUser = user.IdUser, Email = "tracked@example.com" });
        Assert.True(result.IsSuccess);
        Assert.Equal("tracked@example.com", result.User!.Email);
        Assert.Equal(CenterA, result.User.SolutionCenterId);
        Assert.Single(await f.Context.Userroles.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task CreationImportsWordpressEmailOnlyWhenEmailIsOmitted()
    {
        await using var f = new Fixture();
        await f.Initialize();
        var request = Request();
        request.Email = null;
        f.Wordpress.Setup(x => x.GetByLogin(request.UserLogin!)).ReturnsAsync(new WordpressUserDto
        {
            WordpressUserId = request.WordpressUserId, WordpressUserLogin = request.UserLogin,
            WordpressUserEmail = "  imported@example.com  "
        });
        var created = Assert.IsType<UserAdministrationDto>(
            Response<OkObjectResult>(await f.Controller.Create(request)).Result);
        Assert.Equal("imported@example.com", created.Email);
        Assert.Equal(created.Email, (await f.Context.Users.AsNoTracking().SingleAsync()).Email);
        f.Wordpress.Verify(x => x.GetByLogin(request.UserLogin!), Times.Once);
        f.Wordpress.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("invalid")]
    public async Task InvalidWordpressEmailRequiresAnExplicitValidLocalEmail(string? email)
    {
        await using var f = new Fixture();
        await f.Initialize();
        var request = Request();
        request.Email = null;
        f.Wordpress.Setup(x => x.GetByLogin(request.UserLogin!)).ReturnsAsync(new WordpressUserDto
        { WordpressUserId = request.WordpressUserId, WordpressUserEmail = email });
        Assert.False(Response<BadRequestObjectResult>(await f.Controller.Create(request)).IsSuccess);
        Assert.Empty(await f.Context.Users.ToListAsync());
        Assert.Empty(await f.Context.AuthenticationLogs.ToListAsync());
    }

    [Fact]
    public async Task WordpressIdentityMismatchCannotSeedAnotherUsersEmail()
    {
        await using var f = new Fixture();
        await f.Initialize();
        var request = Request();
        request.Email = null;
        f.Wordpress.Setup(x => x.GetByLogin(request.UserLogin!)).ReturnsAsync(new WordpressUserDto
        { WordpressUserId = 999, WordpressUserEmail = "other@example.com" });
        Assert.False(Response<BadRequestObjectResult>(await f.Controller.Create(request)).IsSuccess);
        Assert.Empty(await f.Context.Users.ToListAsync());
    }

    [Fact]
    public async Task ExplicitEmailDoesNotRequireWordpressAndIsNotUnique()
    {
        await using var f = new Fixture();
        await f.Initialize();
        var request = Request(OtherRole);
        request.Email = "  shared@example.com  ";
        Assert.True(Response<OkObjectResult>(await f.Controller.Create(request)).IsSuccess);
        request.UserLogin = "second.user";
        request.WordpressUserId++;
        Assert.True(Response<OkObjectResult>(await f.Controller.Create(request)).IsSuccess);
        Assert.Equal(2, await f.Context.Users.CountAsync(x => x.Email == "shared@example.com"));
        f.Wordpress.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task EmailOnlyPutPreservesRoleCenterAndFalseStatusAndReturnsTheSavedEmail()
    {
        await using var f = new Fixture();
        await f.Initialize();
        var user = await f.Create(OtherRole);
        await f.Users.UpdateUser(user.IdUser, false, OtherRole, CenterA);
        var request = JsonSerializer.Deserialize<UpdateUserDto>(
            JsonSerializer.Serialize(new { idUser = user.IdUser, email = "  changed@example.com  " }, Json), Json)!;
        Assert.Null(request.IdRole);
        Assert.Null(request.StatusUser);
        Assert.Null(request.SolutionCenterId);
        var updated = Assert.IsType<UserAdministrationDto>(
            Response<OkObjectResult>(await f.Controller.Update(request)).Result);
        Assert.Equal("changed@example.com", updated.Email);
        Assert.False(updated.StatusUser);
        Assert.Equal(OtherRole, updated.IdRole);
        Assert.Equal(CenterA, updated.SolutionCenterId);
        var persisted = await f.Context.Users.AsNoTracking().SingleAsync();
        Assert.Equal(updated.Email, persisted.Email);
        Assert.False(persisted.StatusUser);
        var assignment = await f.Context.Userroles.AsNoTracking().SingleAsync();
        Assert.Equal(OtherRole, assignment.IdRole);
        Assert.Equal(CenterA, assignment.SolutionCenterId);
        var page = Assert.IsType<PagedDto<UserAdministrationDto>>(
            Response<OkObjectResult>(await f.Controller.GetAll()).Result);
        Assert.Equal(updated.Email, Assert.Single(page.Items).Email);
        Assert.Contains("Se actualizó el correo electrónico.", (await f.Context.AuthenticationLogs.SingleAsync()).Description);
        f.Wordpress.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("Name <a@example.com>")]
    [InlineData("a@b@example.com")]
    public async Task InvalidEmailCannotChangeUserOrWriteASuccessAudit(string email)
    {
        await using var f = new Fixture();
        await f.Initialize();
        var user = await f.Create();
        Assert.False(Response<BadRequestObjectResult>(await f.Controller.Update(new UpdateUserDto
        { IdUser = user.IdUser, Email = email, StatusUser = false })).IsSuccess);
        var persisted = await f.Context.Users.AsNoTracking().SingleAsync();
        Assert.Equal("initial@example.com", persisted.Email);
        Assert.True(persisted.StatusUser);
        Assert.Empty(await f.Context.AuthenticationLogs.ToListAsync());
    }

    [Theory]
    [InlineData(PdvRole, null)]
    [InlineData(OtherRole, null)]
    [InlineData(OtherRole, 0)]
    [InlineData(OtherRole, -1)]
    [InlineData(OtherRole, 999)]
    [InlineData(PdvRole, 999)]
    public async Task PersistenceRejectsMissingOrUnknownCentersForEveryRole(int role, int? center)
    {
        await using var f = new Fixture();
        await f.Initialize();
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Create(role, center));
        Assert.Empty(await f.Context.Users.AsNoTracking().ToListAsync());
        Assert.Empty(await f.Context.Userroles.AsNoTracking().ToListAsync());
    }

    [Theory]
    [InlineData("Administrador")]
    [InlineData("Operario")]
    [InlineData("Otro rol")]
    public async Task NewlyCreatedRolesCannotBypassTheCenterRule(string name)
    {
        await using var f = new Fixture();
        await f.Initialize();
        f.Context.Roles.Add(new Role { IdRole = 333, NameRole = name, StatusRole = 1 });
        await f.Context.SaveChangesAsync();
        Assert.False(Response<BadRequestObjectResult>(await f.Controller.Create(Request(333, null))).IsSuccess);
        Assert.True(Response<OkObjectResult>(await f.Controller.Create(Request(333, CenterA))).IsSuccess);
    }

    [Theory]
    [InlineData(OtherRole)]
    [InlineData(PdvRole)]
    public async Task RoleChangesRejectUnknownCentersWithoutLosingTheExistingAssignment(int role)
    {
        await using var f = new Fixture();
        await f.Initialize();
        var user = await f.Create();
        Assert.False(Response<BadRequestObjectResult>(await f.Controller.Update(new UpdateUserDto
        { IdUser = user.IdUser, IdRole = role, SolutionCenterId = 999, Email = "new@example.com" })).IsSuccess);
        Assert.Equal(CenterA, (await f.Context.Userroles.AsNoTracking().SingleAsync()).SolutionCenterId);
        Assert.Equal("initial@example.com", (await f.Context.Users.AsNoTracking().SingleAsync()).Email);
    }

    [Fact]
    public async Task NullOptionalFieldsPreserveTheCurrentValidValues()
    {
        await using var f = new Fixture();
        await f.Initialize();
        var user = await f.Create(OtherRole);
        var updated = Assert.IsType<UserAdministrationDto>(Response<OkObjectResult>(
            await f.Controller.Update(new UpdateUserDto { IdUser = user.IdUser })).Result);
        Assert.Equal(CenterA, updated.SolutionCenterId);
        Assert.Equal(OtherRole, updated.IdRole);
        Assert.Equal("initial@example.com", updated.Email);
        Assert.True(updated.StatusUser);
    }

    [Fact]
    public async Task HistoricalUserWithoutAssignmentRequiresAnExplicitRepair()
    {
        await using var f = new Fixture();
        await f.Initialize();
        var legacy = new User { UserLogin = "legacy", UserName = "Historical user", WordpressUserId = 8,
            StatusUser = true, Email = "legacy@example.com" };
        f.Context.Users.Add(legacy);
        await f.Context.SaveChangesAsync();
        Assert.False(Response<BadRequestObjectResult>(await f.Controller.Update(new UpdateUserDto
        { IdUser = legacy.IdUser, Email = "changed@example.com" })).IsSuccess);
        Assert.Equal("legacy@example.com", (await f.Context.Users.AsNoTracking().SingleAsync()).Email);
        Assert.True(Response<OkObjectResult>(await f.Controller.Update(new UpdateUserDto
        { IdUser = legacy.IdUser, IdRole = OtherRole, SolutionCenterId = CenterA, Email = "changed@example.com" })).IsSuccess);
        Assert.Equal(CenterA, (await f.Context.Userroles.AsNoTracking().SingleAsync()).SolutionCenterId);
    }

    [Fact]
    public async Task AssignedRolesCannotBeDeletedOrLeaveUsersWithoutACenter()
    {
        await using var f = new Fixture();
        await f.Initialize();
        await f.Create(OtherRole);
        Assert.False(Response<BadRequestObjectResult>(await f.RoleController.Delete(OtherRole)).IsSuccess);
        Assert.Single(await f.Context.Userroles.AsNoTracking().ToListAsync());
        Assert.Empty(await f.Context.AuthenticationLogs.ToListAsync());
        f.Context.Roles.Remove(await f.Context.Roles.SingleAsync(x => x.IdRole == OtherRole));
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task LoginSsoAndRefreshUseLocalEmailAfterPutAndPreserveAllPreviousClaims()
    {
        await using var f = new Fixture();
        await f.Initialize();
        var user = await f.Create();
        f.Wordpress.Setup(x => x.GetByLogin(user.UserLogin)).ReturnsAsync(new WordpressUserDto
        {
            WordpressUserId = user.WordpressUserId, WordpressUserLogin = user.UserLogin,
            WordpressUserPass = "test-hash", WordpressUserEmail = "wordpress-old@example.com"
        });
        f.Wordpress.Setup(x => x.Verify("test-password", "test-hash")).Returns(true);
        var menus = new Mock<IMenuOptionApplication>();
        menus.Setup(x => x.GetByRole(PdvRole)).ReturnsAsync(new[] { new MenuOptionDto { IdMenuOption = 5 } });
        var sso = new Mock<ISSOSignatureApplication>();
        sso.Setup(x => x.IsValid(user.UserLogin, 123, "test-signature")).Returns(true);
        var sessions = new Mock<ISessionRepository>();
        sessions.Setup(x => x.Create(It.IsAny<AuthenticationSession>())).Returns(Task.CompletedTask);
        sessions.Setup(x => x.RotateRefreshToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync((string sid, string previous, string next) => new AuthenticationSession
            { IdSession = sid, IdUser = user.IdUser, ExpiresAt = DateTime.UtcNow.AddMinutes(5) });
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = new string('X', 64), ["Jwt:Issuer"] = "test-issuer",
            ["Jwt:Audience"] = "test-audience", ["Jwt:ExpirationMinutes"] = "5"
        }).Build();
        var app = new AuthApplication(f.Wordpress.Object, sso.Object, menus.Object,
            new JwtTokenApplication(configuration), f.Users, f.Mapper, sessions.Object,
            Options.Create(new SessionOptions()), TimeProvider.System);
        var before = (await app.Login(new LoginDto { Username = user.UserLogin, Password = "test-password" }))!;
        Assert.Equal("initial@example.com", Email(before));
        Assert.True(Response<OkObjectResult>(await f.Controller.Update(new UpdateUserDto
        { IdUser = user.IdUser, Email = "updated@example.com" })).IsSuccess);

        var login = (await app.Login(new LoginDto { Username = user.UserLogin, Password = "test-password" }))!;
        var intranet = (await app.IntranetAccess(new IntranetAccessDto
        { UserLogin = user.UserLogin, Ts = 123, Sig = "test-signature" }))!;
        var refreshed = await app.Refresh(before.RefreshToken!);
        var previousJwt = new JwtSecurityTokenHandler().ReadJwtToken(before.AccessToken);
        foreach (var response in new[] { login, intranet, refreshed })
        {
            Assert.Equal("updated@example.com", response.User!.Email);
            Assert.Equal("updated@example.com", Email(response));
            var token = new JwtSecurityTokenHandler().ReadJwtToken(response.AccessToken);
            foreach (var claim in new[] { "sub", "idUser", "wordpressUserId", "userLogin", "userName", "idRole", "nameRole", "iss", "aud" })
                Assert.Equal(previousJwt.Payload[claim], token.Payload[claim]);
            Assert.True(Guid.TryParse(token.Claims.Single(x => x.Type == "sid").Value, out _));
            Assert.NotNull(token.Payload.Expiration);
        }
        Assert.Equal(previousJwt.Claims.Single(x => x.Type == "sid").Value,
            new JwtSecurityTokenHandler().ReadJwtToken(refreshed.AccessToken).Claims.Single(x => x.Type == "sid").Value);
        Assert.Equal("initial@example.com", Email(before)); // Already-issued tokens are immutable.
        Assert.Equal("updated@example.com", (await f.Context.Users.AsNoTracking().SingleAsync()).Email);
        Assert.Single(await f.Context.Userroles.AsNoTracking().ToListAsync());

        static string Email(AuthUserDto result) => new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken)
            .Claims.Single(x => x.Type == "email").Value;
    }
}
