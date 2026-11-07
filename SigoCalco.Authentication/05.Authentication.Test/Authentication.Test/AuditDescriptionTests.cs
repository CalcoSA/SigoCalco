using System.Security.Claims;
using System.Text.Json;
using SigoCalco.Authentication.Api.Controllers;
using SigoCalco.Authentication.Application.Interfaces;
using SigoCalco.Authentication.Application.Services;
using SigoCalco.Authentication.Domain.Dtos;
using SigoCalco.Authentication.Domain.Interfaces;
using SigoCalco.Authentication.Domain.Models;
using SigoCalco.Authentication.Domain.Responses;
using SigoCalco.Authentication.Domain.Validators;
using AutoMapper;
using SigoCalco.Authentication.Infrastructure.Persistence.Mappings;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace SigoCalco.Authentication.Test;

public class AuditDescriptionTests
{
    private const string Actor = "administrador.operaciones";

    private sealed class Fixture
    {
        public Mock<IUserRepository> Users { get; } = new(MockBehavior.Strict);
        public Mock<IRoleRepository> Roles { get; } = new(MockBehavior.Strict);
        public Mock<ILogRepository> Logs { get; } = new(MockBehavior.Strict);
        public List<AuthenticationLog> WrittenLogs { get; } = new();
        public Task Persistence { get; set; } = Task.CompletedTask;
        public User AffectedUser { get; } = new()
        {
            IdUser = 42, UserLogin = "agomez", UserName = "Ana María Gómez", Email = "ana@example.com", StatusUser = true,
            Userroles = new[] { new Userrole { IdRole = 7, SolutionCenterId = 101, IdRoleNavigation = new Role { IdRole = 7, NameRole = "Supervisión" } } }
        };
        public Role AffectedRole { get; } = new() { IdRole = 7, NameRole = "Supervisión", StatusRole = 1 };
        public UserController UserController { get; }
        public RoleController RoleController { get; }

        public Fixture()
        {
            Users.Setup(x => x.GetById(42)).ReturnsAsync(AffectedUser);
            Users.Setup(x => x.GetAdministrativeUserById(42)).ReturnsAsync(AffectedUser);
            Users.Setup(x => x.ExistsByUserLogin("agomez", null)).ReturnsAsync(false);
            Users.Setup(x => x.CreateUser(It.Is<User>(u => u.UserName == "Ana María Gómez" && u.UserLogin == "agomez"), 7, 101))
                .Returns(async (User user, int role, int? center) =>
                {
                    await Persistence;
                    user.IdUser = 42;
                    AffectedUser.StatusUser = user.StatusUser;
                    return new UserAssignmentResultDto();
                });
            Users.Setup(x => x.UpdateUser(42, It.IsAny<bool?>(), 7, null, null))
                .Returns(async (int id, bool? active, int? role, int? center, string? email) =>
                {
                    await Persistence;
                    AffectedUser.StatusUser = active ?? AffectedUser.StatusUser;
                    return new UserAssignmentResultDto();
                });
            Users.Setup(x => x.DeleteUser(42))
                .Callback(() => AffectedUser.UserName = string.Empty)
                .Returns(() => Persistence);
            Roles.Setup(x => x.GetById(7)).ReturnsAsync(AffectedRole);
            Roles.Setup(x => x.ExistsByName("  Coordinación  ", null)).ReturnsAsync(false);
            Roles.Setup(x => x.ExistsByName("  Coordinación  ", 7)).ReturnsAsync(false);
            Roles.Setup(x => x.MenuOptionsExist(It.IsAny<IEnumerable<int>>())).ReturnsAsync(true);
            Roles.Setup(x => x.CreateRole(It.Is<Role>(r => r.NameRole == "Coordinación"), It.IsAny<IEnumerable<int>>()))
                .Returns(() => Persistence);
            Roles.Setup(x => x.UpdateRole(It.Is<Role>(r => r.NameRole == "Coordinación" && r.IdRole == 7), It.IsAny<IEnumerable<int>>()))
                .Returns(() => Persistence);
            Roles.Setup(x => x.DeleteRole(7))
                .Callback(() => AffectedRole.NameRole = string.Empty)
                .Returns(() => Persistence);
            Logs.Setup(x => x.CreateLog(It.IsAny<AuthenticationLog>()))
                .Callback<AuthenticationLog>(WrittenLogs.Add).ReturnsAsync(1L);

            var mapper = new MapperConfiguration(cfg => cfg.AddProfile<AutoMapperProfile>(), NullLoggerFactory.Instance).CreateMapper();
            var logApplication = new LogApplication(Logs.Object);
            UserController = new UserController(new CreateUserDtoValidator(), new UpdateUserDtoValidator(),
                new UserApplication(Users.Object, mapper, Roles.Object, Mock.Of<IWordpressUserRepository>()), logApplication, NullLogger<UserController>.Instance)
            { ControllerContext = Context() };
            RoleController = new RoleController(new CreateRoleDtoValidator(), new UpdateRoleDtoValidator(),
                new RoleApplication(Roles.Object, mapper), logApplication, NullLogger<RoleController>.Instance)
            { ControllerContext = Context() };
        }

        private static ControllerContext Context() => new()
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("userLogin", Actor) }, "Test"))
            }
        };

        public Task<IActionResult> Execute(string operation, bool active = true) => operation switch
        {
            "user-create" => UserController.Create(new CreateUserDto
            {
                WordpressUserId = 123, UserLogin = "  agomez  ", UserName = "  Ana María Gómez  ", IdRole = 7, StatusUser = active, SolutionCenterId = 101, Email = "ana@example.com"
            }),
            "user-update" => UserController.Update(new UpdateUserDto { IdUser = 42, IdRole = 7, StatusUser = active }),
            "user-delete" => UserController.Delete(42),
            "role-create" => RoleController.Create(new CreateRoleDto
            {
                NameRole = "  Coordinación  ", StatusRole = (sbyte)(active ? 1 : 0), MenuOptionIds = new List<int> { 5 }
            }),
            "role-update" => RoleController.Update(new UpdateRoleDto
            {
                IdRole = 7, NameRole = "  Coordinación  ", StatusRole = (sbyte)(active ? 1 : 0), MenuOptionIds = new List<int> { 5 }
            }),
            "role-delete" => RoleController.Delete(7),
            _ => throw new ArgumentOutOfRangeException(nameof(operation))
        };
    }

    [Theory]
    [InlineData("user-create", "Crear", "ConfiguracionUsuarios", "Se creó el usuario Ana María Gómez con el rol Supervisión.")]
    [InlineData("user-update", "Actualizar", "ConfiguracionUsuarios", "Se actualizó el usuario Ana María Gómez: rol Supervisión y estado activo.")]
    [InlineData("user-delete", "Eliminar", "ConfiguracionUsuarios", "Se eliminó el usuario Ana María Gómez.")]
    [InlineData("role-create", "Crear", "ConfiguracionRoles", "Se creó el rol Coordinación con sus opciones de menú.")]
    [InlineData("role-update", "Actualizar", "ConfiguracionRoles", "Se actualizó el rol Coordinación: estado activo y opciones de menú.")]
    [InlineData("role-delete", "Eliminar", "ConfiguracionRoles", "Se eliminó el rol Supervisión y sus opciones de menú asociadas.")]
    public async Task WritesRealNamesOnlyAfterPersistenceAndKeepsActorSeparate(
        string operation, string action, string module, string description)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var fixture = new Fixture { Persistence = completion.Task };

        var pending = fixture.Execute(operation);
        Assert.False(pending.IsCompleted);
        Assert.Empty(fixture.WrittenLogs);
        completion.SetResult();

        var result = Assert.IsType<OkObjectResult>(await pending);
        var response = Assert.IsType<ResponseApi>(result.Value);
        Assert.True(response.IsSuccess);
        if (operation is "user-create" or "user-update")
            Assert.Equal("ana@example.com", Assert.IsType<UserAdministrationDto>(response.Result).Email);
        else
            Assert.Empty(JsonSerializer.SerializeToElement(response.Result).EnumerateObject());
        var log = Assert.Single(fixture.WrittenLogs);
        Assert.Equal(description, log.Description);
        Assert.Equal(Actor, log.UserName);
        Assert.Equal(action, log.Action);
        Assert.Equal(module, log.Module);

        // Los nombres proceden de las consultas que ya validan la operación.
        fixture.Users.Verify(x => x.GetById(42), operation is "user-update" or "user-delete" ? Times.Once() : Times.Never());
        fixture.Roles.Verify(x => x.GetById(7), operation is "user-delete" or "role-create" ? Times.Never() : Times.Once());
        fixture.Users.Verify(x => x.GetUserById(It.IsAny<int>()), Times.Never());
        fixture.Roles.Verify(x => x.GetByIdRole(It.IsAny<int>()), Times.Never());
    }

    [Theory]
    [InlineData("user-update", "Se actualizó el usuario Ana María Gómez: rol Supervisión y estado inactivo.")]
    [InlineData("role-update", "Se actualizó el rol Coordinación: estado inactivo y opciones de menú.")]
    public async Task UsesTheNewStatusAndRoleName(string operation, string description)
    {
        var fixture = new Fixture();
        Assert.IsType<OkObjectResult>(await fixture.Execute(operation, active: false));
        Assert.Equal(description, Assert.Single(fixture.WrittenLogs).Description);
    }

    [Theory]
    [InlineData("user-create")]
    [InlineData("user-update")]
    [InlineData("user-delete")]
    [InlineData("role-create")]
    [InlineData("role-update")]
    [InlineData("role-delete")]
    public async Task FailedPersistenceNeverWritesSuccessLog(string operation)
    {
        var fixture = new Fixture { Persistence = Task.FromException(new InvalidOperationException("Persistence failed")) };
        var result = Assert.IsType<ObjectResult>(await fixture.Execute(operation));
        Assert.Equal(500, result.StatusCode);
        Assert.False(Assert.IsType<ResponseApi>(result.Value).IsSuccess);
        Assert.Empty(fixture.WrittenLogs);
        fixture.Logs.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("user-create", 400)]
    [InlineData("user-update", 400)]
    [InlineData("user-delete", 404)]
    [InlineData("role-create", 400)]
    [InlineData("role-update", 400)]
    [InlineData("role-delete", 404)]
    public async Task RejectedOperationsNeverWriteSuccessLog(string operation, int expectedStatus)
    {
        var fixture = new Fixture();
        fixture.Users.Setup(x => x.GetById(42)).ReturnsAsync((User)null!);
        fixture.Roles.Setup(x => x.GetById(7)).ReturnsAsync((Role)null!);
        fixture.Roles.Setup(x => x.ExistsByName("  Coordinación  ", null)).ReturnsAsync(true);

        var result = Assert.IsAssignableFrom<ObjectResult>(await fixture.Execute(operation));
        Assert.Equal(expectedStatus, result.StatusCode);
        Assert.False(Assert.IsType<ResponseApi>(result.Value).IsSuccess);
        Assert.Empty(fixture.WrittenLogs);
        fixture.Logs.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UpdatingUserWithMissingNewRoleNeverWritesSuccessLog()
    {
        var fixture = new Fixture();
        fixture.Roles.Setup(x => x.GetById(7)).ReturnsAsync((Role)null!);

        Assert.IsType<BadRequestObjectResult>(await fixture.Execute("user-update"));
        Assert.Empty(fixture.WrittenLogs);
        fixture.Users.Verify(x => x.UpdateUser(It.IsAny<int>(), It.IsAny<bool?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<string?>()), Times.Never());
    }

    [Theory]
    [InlineData(false, "success", 200)]
    [InlineData(true, "success", 200)]
    [InlineData(false, "rejected", 401)]
    [InlineData(true, "rejected", 401)]
    [InlineData(false, "failed", 500)]
    [InlineData(true, "failed", 500)]
    public async Task AuthenticationKeepsItsAuditAndOnlyLogsSuccessfulAccess(bool intranet, string outcome, int status)
    {
        var auth = new Mock<IAuthApplication>(MockBehavior.Strict);
        var logs = new Mock<ILogRepository>(MockBehavior.Strict);
        var writtenLogs = new List<AuthenticationLog>();
        var authenticated = new AuthUserDto
        {
            User = new UserDto { IdUser = 42, UserLogin = "agomez", UserName = "Ana María Gómez" },
            AccessToken = "test-access-secret", RefreshToken = "test-refresh-secret"
        };
        Task<AuthUserDto?> Authenticate() => outcome == "failed"
            ? Task.FromException<AuthUserDto?>(new InvalidOperationException("Authentication failed"))
            : Task.FromResult(outcome == "success" ? authenticated : null);
        auth.Setup(x => x.Login(It.IsAny<LoginDto>())).Returns(Authenticate);
        auth.Setup(x => x.IntranetAccess(It.IsAny<IntranetAccessDto>())).Returns(Authenticate);
        logs.Setup(x => x.CreateLog(It.IsAny<AuthenticationLog>()))
            .Callback<AuthenticationLog>(writtenLogs.Add).ReturnsAsync(1L);
        var controller = new AuthController(auth.Object, new LogApplication(logs.Object), NullLogger<AuthController>.Instance);

        var result = intranet
            ? await controller.IntranetAccess("requested-login", 123, "test-signature-secret")
            : await controller.Login(new LoginDto { Username = "requested-login", Password = "test-password-secret" });

        Assert.Equal(status, Assert.IsAssignableFrom<ObjectResult>(result).StatusCode);
        if (outcome == "success")
        {
            var log = Assert.Single(writtenLogs);
            Assert.Equal("agomez", log.UserName);
            Assert.Equal("IniciarSesion", log.Action);
            Assert.Equal("Autenticacion", log.Module);
            Assert.Equal(intranet ? "Se inició sesión correctamente desde intranet." : "Se inició sesión correctamente.", log.Description);
        }
        else
        {
            Assert.Empty(writtenLogs);
            logs.VerifyNoOtherCalls();
        }
    }
}
