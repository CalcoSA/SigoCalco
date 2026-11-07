using System.Data.Common;
using System.IdentityModel.Tokens.Jwt;
using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Domain.Interfaces;
using SigoCalco.Authentication.Domain.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Moq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using SigoCalco.Authentication.Api.Controllers;
using SigoCalco.Authentication.Application.Services;
using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Exceptions;
using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Domain.Responses;
using SigoCalco.Authentication.Domain.Validators;
using SigoCalco.Authentication.Infrastructure.Persistence.Data;
using SigoCalco.Authentication.Infrastructure.Persistence.Mappings;
using SigoCalco.Authentication.Infrastructure.Persistence.Repositories;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Logging.Abstractions;

namespace SigoCalco.Authentication.Test;

public partial class PointOfSaleAssignmentTests
{
    // Deliberately different from production ID 6. Catalog entries exist only in memory.
    private const int PdvRole = 91;
    private const int OtherRole = 7;
    private const int CenterA = 101;
    private const int CenterB = 202;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private sealed class Commands : DbCommandInterceptor
    {
        public bool FailAssignmentInsert { get; set; }
        public bool FailureInjected { get; private set; }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            // SQLite verifies real commits, rollbacks and FKs, but has no MySQL row locks.
            // Assert their transactional placement and remove only their SQL syntax.
            if (command.CommandText.Contains(" FOR UPDATE") || command.CommandText.Contains(" LOCK IN SHARE MODE"))
            {
                Assert.NotNull(eventData.Context!.Database.CurrentTransaction);
                command.CommandText = command.CommandText.Replace(" FOR UPDATE", "")
                    .Replace(" LOCK IN SHARE MODE", "");
            }
            if (FailAssignmentInsert && command.CommandText.Contains("INSERT INTO \"sigocalco_authentication_userrole\""))
            {
                FailureInjected = true;
                throw new InvalidOperationException("Injected assignment persistence failure.");
            }
            return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
        }
    }

    private sealed class TestContext(DbContextOptions<SigoCalcoContext> options) : SigoCalcoContext(options)
    {
        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            // SQLite requires this exact type name for an auto-increment primary key.
            builder.Entity<SolutionCenter>().Property(x => x.SolutionCenterId).HasColumnType("INTEGER");
            // SQLite does not accept MySQL's fractional precision argument.
            builder.Entity<AuthenticationLog>().Property(x => x.CreatedAt).HasDefaultValueSql("CURRENT_TIMESTAMP");
        }
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection _connection = new("Data Source=:memory:");
        public Commands Commands { get; } = new();
        public TestContext Context { get; private set; } = null!;
        public IMapper Mapper { get; } = new MapperConfiguration(
            cfg => cfg.AddProfile<AutoMapperProfile>(), NullLoggerFactory.Instance).CreateMapper();
        public UserRepository Users { get; private set; } = null!;
        public Mock<IWordpressUserRepository> Wordpress { get; } = new(MockBehavior.Strict);
        public RoleRepository Roles { get; private set; } = null!;
        public UserController Controller { get; private set; } = null!;
        public RoleController RoleController { get; private set; } = null!;

        public async Task Initialize()
        {
            await _connection.OpenAsync();
            _connection.CreateCollation("ascii_bin", (a, b) => string.CompareOrdinal(a, b));
            Context = new TestContext(new DbContextOptionsBuilder<SigoCalcoContext>()
                .UseSqlite(_connection).AddInterceptors(Commands).Options);
            await Context.Database.EnsureCreatedAsync();
            Context.Roles.AddRange(
                new Role { IdRole = PdvRole, NameRole = "PDV", RoleCode = Role.PointOfSaleCode, StatusRole = 1 },
                new Role { IdRole = OtherRole, NameRole = "Consulta", StatusRole = 1 });
            Context.SolutionCenters.AddRange(
                new SolutionCenter { SolutionCenterId = CenterA, SolutionCenterCode = "TEST-A", SolutionCenterName = "Solo prueba A" },
                new SolutionCenter { SolutionCenterId = CenterB, SolutionCenterCode = "TEST-B", SolutionCenterName = "Solo prueba B" });
            Context.Menuoptions.Add(new Menuoption { IdMenuOption = 5, NameMenuOption = "Prueba", StatusMenuOption = 1 });
            Context.Rolemenuoptions.Add(new Rolemenuoption { IdRole = PdvRole, IdMenuOption = 5 });
            await Context.SaveChangesAsync();
            Context.ChangeTracker.Clear();
            Users = new UserRepository(Context);
            Roles = new RoleRepository(Context);
            var logs = new LogApplication(new LogRepository(Context));
            Controller = new UserController(new CreateUserDtoValidator(), new UpdateUserDtoValidator(),
                new UserApplication(Users, Mapper, Roles, Wordpress.Object), logs, NullLogger<UserController>.Instance)
            { ControllerContext = Actor() };
            RoleController = new RoleController(new CreateRoleDtoValidator(), new UpdateRoleDtoValidator(),
                new RoleApplication(Roles, Mapper), logs, NullLogger<RoleController>.Instance)
            { ControllerContext = Actor() };
        }

        private static ControllerContext Actor() => new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userLogin", "test.admin") }, "Test"))
            }
        };

        public async Task<User> Create(int role = PdvRole, int? center = CenterA, string login = "test.user")
        {
            var user = new User { WordpressUserId = (ulong)(await Context.Users.CountAsync() + 1),
                UserLogin = login, UserName = "Usuario de prueba", StatusUser = true, Email = "initial@example.com" };
            await Users.CreateUser(user, role, center);
            Context.ChangeTracker.Clear();
            return user;
        }

        public async ValueTask DisposeAsync()
        {
            if (Context is not null) await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private static CreateUserDto Request(int role = PdvRole, int? center = CenterA) => new()
    {
        WordpressUserId = 123, UserLogin = "test.request", UserName = "Usuario solicitado",
        StatusUser = true, IdRole = role, SolutionCenterId = center, Email = "initial@example.com"
    };

    private static ResponseApi Response<T>(IActionResult result) where T : ObjectResult
        => Assert.IsType<ResponseApi>(Assert.IsType<T>(result).Value);

    [Fact]
    public async Task CreatesPdvThroughControllerAndAllowsSharedCenter()
    {
        await using var f = new Fixture();
        await f.Initialize();
        var response = Response<OkObjectResult>(await f.Controller.Create(Request()));
        Assert.True(response.IsSuccess);
        Assert.Equal("initial@example.com", Assert.IsType<UserAdministrationDto>(response.Result).Email);
        var log = await f.Context.AuthenticationLogs.SingleAsync();
        Assert.Contains("Se asignó el Centro de Soluciones TEST-A.", log.Description);
        Assert.Equal("test.admin", log.UserName);
        await f.Create(login: "test.second");
        Assert.Equal(2, await f.Context.Userroles.CountAsync(x => x.SolutionCenterId == CenterA));
        Assert.Equal(2, await f.Context.SolutionCenters.CountAsync());
    }

    [Theory]
    [InlineData(null, "Debe seleccionar")]
    [InlineData(0, "mayor a cero")]
    [InlineData(-1, "mayor a cero")]
    [InlineData(999, "no existe")]
    public async Task InvalidPdvCreationReturns400AndWritesNothing(int? center, string message)
    {
        await using var f = new Fixture();
        await f.Initialize();
        var response = Response<BadRequestObjectResult>(await f.Controller.Create(Request(center: center)));
        Assert.False(response.IsSuccess);
        Assert.Contains(message, response.Message + JsonSerializer.Serialize(response.Result));
        Assert.Empty(await f.Context.Users.AsNoTracking().ToListAsync());
        Assert.Empty(await f.Context.Userroles.AsNoTracking().ToListAsync());
        Assert.Empty(await f.Context.AuthenticationLogs.ToListAsync());
        Assert.Equal(2, await f.Context.SolutionCenters.CountAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(999)]
    public async Task InvalidPdvUpdateReturns400AndPreservesExistingAssignment(int? center)
    {
        await using var f = new Fixture();
        await f.Initialize();
        var user = await f.Create();
        var response = Response<BadRequestObjectResult>(await f.Controller.Update(new UpdateUserDto
        { IdUser = user.IdUser, IdRole = PdvRole, StatusUser = false, SolutionCenterId = center }));
        Assert.False(response.IsSuccess);
        Assert.True((await f.Context.Users.AsNoTracking().SingleAsync()).StatusUser);
        Assert.Equal(CenterA, (await f.Context.Userroles.AsNoTracking().SingleAsync()).SolutionCenterId);
        Assert.Empty(await f.Context.AuthenticationLogs.ToListAsync());
    }

    [Fact]
    public async Task OtherRolesKeepARequiredCenterInTheirAdministrativeJson()
    {
        await using var f = new Fixture();
        await f.Initialize();
        Assert.True(Response<OkObjectResult>(await f.Controller.Create(Request(OtherRole, CenterA))).IsSuccess);
        var user = await f.Context.Users.AsNoTracking().SingleAsync();
        Assert.Equal(CenterA, (await f.Context.Userroles.AsNoTracking().SingleAsync()).SolutionCenterId);
        var administrative = Assert.IsType<UserAdministrationDto>(
            Response<OkObjectResult>(await f.Controller.GetById(user.IdUser)).Result);
        Assert.Equal(CenterA, administrative.SolutionCenterId);
        Assert.Equal("TEST-A", administrative.SolutionCenterCode);
        Assert.True(Response<OkObjectResult>(await f.Controller.Update(new UpdateUserDto
        { IdUser = user.IdUser, IdRole = OtherRole, SolutionCenterId = CenterB })).IsSuccess);
        Assert.Equal(CenterB, (await f.Context.Userroles.AsNoTracking().SingleAsync()).SolutionCenterId);
    }

    [Theory]
    [InlineData(OtherRole, CenterB, PdvRole, CenterA, "Se cambió el Centro de Soluciones de TEST-B a TEST-A.")]
    [InlineData(PdvRole, CenterA, PdvRole, CenterB, "Se cambió el Centro de Soluciones de TEST-A a TEST-B.")]
    [InlineData(PdvRole, CenterA, OtherRole, CenterB, "Se cambió el Centro de Soluciones de TEST-A a TEST-B.")]
    [InlineData(PdvRole, CenterA, OtherRole, null, "Se actualizó el usuario")]
    public async Task TransitionsKeepOneRoleAndAuditTheAssignment(int oldRole, int? oldCenter,
        int newRole, int? newCenter, string audit)
    {
        await using var f = new Fixture();
        await f.Initialize();
        var user = await f.Create(oldRole, oldCenter);
        Assert.True(Response<OkObjectResult>(await f.Controller.Update(new UpdateUserDto
        { IdUser = user.IdUser, IdRole = newRole, StatusUser = false, SolutionCenterId = newCenter })).IsSuccess);
        var assignment = await f.Context.Userroles.AsNoTracking().SingleAsync();
        Assert.Equal(newRole, assignment.IdRole);
        Assert.Equal(newCenter ?? oldCenter, assignment.SolutionCenterId);
        Assert.False((await f.Context.Users.AsNoTracking().SingleAsync()).StatusUser);
        Assert.Contains(audit, (await f.Context.AuthenticationLogs.SingleAsync()).Description);
    }

    [Fact]
    public async Task AdministrativeReadsIncludeTheCatalogWithoutChangingAuthenticationDto()
    {
        await using var f = new Fixture();
        await f.Initialize();
        var user = await f.Create();
        await f.Create(OtherRole, CenterB, "another.user");
        var detail = Assert.IsType<UserAdministrationDto>(Response<OkObjectResult>(await f.Controller.GetById(user.IdUser)).Result);
        Assert.Equal(CenterA, detail.SolutionCenterId);
        Assert.Equal("TEST-A", detail.SolutionCenterCode);
        Assert.Equal("Solo prueba A", detail.SolutionCenterName);
        var page = Assert.IsType<PagedDto<UserAdministrationDto>>(
            Response<OkObjectResult>(await f.Controller.GetAll(1, 1, "TEST.USER")).Result);
        Assert.Equal(1, page.Total);
        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.Take);
        Assert.Equal(1, page.Pages);
        Assert.Equal("TEST-A", Assert.Single(page.Items).SolutionCenterCode);
        foreach (var source in new[] { await f.Users.GetUserByLogin(user.UserLogin), await f.Users.GetUserById(user.IdUser) })
        {
            var dto = Assert.IsType<UserDto>(f.Mapper.Map<UserDto>(source));
            var auth = JsonSerializer.SerializeToElement(new AuthUserDto { User = dto }, Json);
            Assert.False(auth.GetProperty("user").TryGetProperty("solutionCenterId", out _));
            Assert.Equal(PdvRole, auth.GetProperty("user").GetProperty("idRole").GetInt32());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PersistenceFailureRollsBackUserAndAssignmentAndDoesNotAudit(bool update)
    {
        await using var f = new Fixture();
        await f.Initialize();
        var existing = update ? await f.Create() : null;
        var originalRoleId = update ? (await f.Context.Userroles.AsNoTracking().SingleAsync()).IdUserRole : 0;
        f.Commands.FailAssignmentInsert = true;
        var result = update
            ? await f.Controller.Update(new UpdateUserDto
                { IdUser = existing!.IdUser, IdRole = OtherRole, StatusUser = false })
            : await f.Controller.Create(Request());
        Assert.Equal(500, Assert.IsType<ObjectResult>(result).StatusCode);
        Assert.True(f.Commands.FailureInjected);
        f.Context.ChangeTracker.Clear();
        Assert.Empty(await f.Context.AuthenticationLogs.ToListAsync());
        if (update)
        {
            Assert.True((await f.Context.Users.SingleAsync()).StatusUser);
            var assignment = await f.Context.Userroles.SingleAsync();
            Assert.Equal(originalRoleId, assignment.IdUserRole);
            Assert.Equal(PdvRole, assignment.IdRole);
            Assert.Equal(CenterA, assignment.SolutionCenterId);
        }
        else
        {
            Assert.Empty(await f.Context.Users.ToListAsync());
            Assert.Empty(await f.Context.Userroles.ToListAsync());
        }
    }

    [Fact]
    public async Task PdvRoleCodeIsReadOnlyAndDeletionIsRejectedWithoutChangingMenus()
    {
        await using var f = new Fixture();
        await f.Initialize();
        var roles = new RoleApplication(f.Roles, f.Mapper);
        Assert.Equal("PDV", (await roles.GetById(PdvRole)).RoleCode);
        Assert.Equal("PDV", (await roles.GetAll()).Single(x => x.IdRole == PdvRole).RoleCode);
        var request = JsonSerializer.Deserialize<UpdateRoleDto>(
            $$"""{"idRole":{{PdvRole}},"nameRole":"PDV","statusRole":1,"menuOptionIds":[5],"roleCode":"OTHER"}""", Json)!;
        Assert.True(Response<OkObjectResult>(await f.RoleController.Update(request)).IsSuccess);
        Assert.Equal("PDV", (await f.Context.Roles.AsNoTracking().SingleAsync(x => x.IdRole == PdvRole)).RoleCode);
        var menu = await f.Context.Rolemenuoptions.AsNoTracking().SingleAsync();
        Assert.Equal(5, menu.IdMenuOption);
        var response = Response<BadRequestObjectResult>(await f.RoleController.Delete(PdvRole));
        Assert.Contains("No se puede eliminar", response.Message);
        await Assert.ThrowsAsync<BusinessRuleException>(() => f.Roles.DeleteRole(PdvRole));
        Assert.Equal(1, await f.Context.Rolemenuoptions.CountAsync());
        Assert.DoesNotContain(await f.Context.AuthenticationLogs.ToListAsync(), x => x.Action == "Eliminar");
    }

    [Fact]
    public async Task CatalogIsOrderedReadOnlyAndProtectedAndForeignKeyIsRestrictive()
    {
        await using var f = new Fixture();
        await f.Initialize();
        var app = new SolutionCenterApplication(new SolutionCenterRepository(f.Context), f.Mapper);
        var controller = new SolutionCentersController(app, NullLogger<SolutionCentersController>.Instance);
        var response = Response<OkObjectResult>(await controller.GetAll());
        var centers = Assert.IsAssignableFrom<IEnumerable<SolutionCenterDto>>(response.Result).ToList();
        Assert.Equal(new[] { "TEST-A", "TEST-B" }, centers.Select(x => x.SolutionCenterCode));
        var type = typeof(SolutionCentersController);
        Assert.True(Attribute.IsDefined(type, typeof(AuthorizeAttribute)));
        Assert.Equal("api/Authentication/SolutionCenters", ((RouteAttribute)Attribute.GetCustomAttribute(type, typeof(RouteAttribute))!).Template);
        Assert.Single(type.GetMethods(), x => Attribute.IsDefined(x, typeof(HttpGetAttribute)));
        Assert.DoesNotContain(type.GetMethods(), x => Attribute.IsDefined(x, typeof(HttpPostAttribute))
            || Attribute.IsDefined(x, typeof(HttpPutAttribute)) || Attribute.IsDefined(x, typeof(HttpDeleteAttribute)));
        await f.Create();
        f.Context.SolutionCenters.Remove(await f.Context.SolutionCenters.SingleAsync(x => x.SolutionCenterId == CenterA));
        await Assert.ThrowsAsync<DbUpdateException>(() => f.Context.SaveChangesAsync());
    }

    [Fact]
    public void MysqlModelMatchesTheMigrationAndKeepsExistingUserRoleUniqueness()
    {
        using var context = new SigoCalcoContext(new DbContextOptionsBuilder<SigoCalcoContext>()
            .UseMySql("Server=localhost;Database=unused;User ID=unused;Password=unused;",
                new MySqlServerVersion(new Version(8, 0, 36))).Options);
        var center = context.Model.FindEntityType(typeof(SolutionCenter))!;
        Assert.Equal("sigocalco_authentication_solution_center", center.GetTableName());
        Assert.Equal(ValueGenerated.OnAdd, center.FindProperty(nameof(SolutionCenter.SolutionCenterId))!.ValueGenerated);
        Assert.Equal("int", center.FindProperty(nameof(SolutionCenter.SolutionCenterId))!.GetColumnType());
        Assert.Equal(64, center.FindProperty(nameof(SolutionCenter.SolutionCenterCode))!.GetMaxLength());
        Assert.Equal(200, center.FindProperty(nameof(SolutionCenter.SolutionCenterName))!.GetMaxLength());
        var userRole = context.Model.FindEntityType(typeof(Userrole))!;
        Assert.False(userRole.FindProperty(nameof(Userrole.SolutionCenterId))!.IsNullable);
        Assert.DoesNotContain(userRole.GetIndexes(), x => x.IsUnique && x.Properties.Count == 1);
        Assert.Contains(userRole.GetIndexes(), x => x.IsUnique && x.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { nameof(Userrole.IdUser), nameof(Userrole.IdRole) }));
        Assert.Equal(DeleteBehavior.Restrict, userRole.GetForeignKeys()
            .Single(x => x.PrincipalEntityType.ClrType == typeof(SolutionCenter)).DeleteBehavior);
    }
}
