using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Meetings.Application.Interfaces;
using Meetings.Domain.Exceptions;
using Meetings.Domain.Models.Actas;
using Meetings.Infrastructure.Persistence.Data;
using Meetings.Infrastructure.Persistence.Mappings;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;

namespace Meetings.Test;

public sealed class TestMeetingsContext(DbContextOptions<MeetingsDbContext> options) : MeetingsDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        ActasModel.Configure(modelBuilder);
        foreach (var property in modelBuilder.Model.GetEntityTypes().SelectMany(t => t.GetProperties()))
        {
            if (property.ClrType == typeof(string)) property.SetColumnType("TEXT");
            else property.SetColumnType(null);
        }
    }
}

public sealed class StubDirectory : IAuthenticationDirectory
{
    public Task<MeetingsIdentity> ResolveAsync(int userId, int roleId, string roleName, CancellationToken ct)
    {
        if (userId != 1 || roleId != 1 || roleName != "Administrador") throw MeetingsRequestException.Forbidden();
        return Task.FromResult(new MeetingsIdentity(1, [" Administrador "], ["R01"]));
    }
}

public sealed class MeetingsApiFactory : WebApplicationFactory<Program>
{
    public Action<IServiceCollection>? Customize { get; set; }
    public CommandCounter Commands { get; } = new();
    public const string Secret = "tests-only-signing-secret-not-a-real-credential-0123456789";
    private readonly SqliteConnection connection = new("Data Source=:memory:");
    public MeetingsApiFactory()
    {
        connection.Open();
        connection.CreateFunction<string, int>("CHAR_LENGTH", x => x.Length);
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:MeetingsDatabase"] = "Server=127.0.0.1;Database=never_connect_tests;",
            ["Jwt:Secret"] = Secret, ["Jwt:Issuer"] = "meetings-tests", ["Jwt:Audience"] = "meetings-tests"
        }));
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<MeetingsDbContext>();
            services.RemoveAll<DbContextOptions<MeetingsDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<MeetingsDbContext>>();
            services.AddScoped<MeetingsDbContext>(_ => new TestMeetingsContext(
                new DbContextOptionsBuilder<MeetingsDbContext>().UseSqlite(connection).AddInterceptors(Commands).Options));
            services.RemoveAll<IAuthenticationDirectory>();
            services.AddScoped<IAuthenticationDirectory, StubDirectory>();
            Customize?.Invoke(services);
        });
    }
    public HttpClient Client(bool authenticated = true)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        if (authenticated) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", Token());
        using var scope = Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<MeetingsDbContext>();
        if (context.Database.EnsureCreated())
        {
            context.AddRange(new ActaType { ActaTypeId = 1, Code = "REUNION", Name = "Acta de reunión" },
                new ActaType { ActaTypeId = 2, Code = "DISCIPLINARIA", Name = "Acta disciplinaria" },
                new ActaType { ActaTypeId = 3, Code = "ADMINISTRATIVA", Name = "Acta administrativa" });
            foreach (var code in new[] { "REUNION", "DISCIPLINARIA", "ADMINISTRATIVA" })
                foreach (var operation in new[] { "PLANTILLA_CREACION", "PLANTILLA_LECTURA", "VERSION_CREACION" })
                    context.Add(new MeetingsPermission { PermissionKey = $"ACTA_{code}_{operation}", PermissionValue = "ADMINISTRADOR" });
            context.SaveChanges();
        }
        return client;
    }
    public static string Token(string issuer = "meetings-tests", string audience = "meetings-tests", bool expired = false,
        string secret = Secret, string idUser = "1", string sub = "1", string role = "Administrador") =>
        new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(issuer, audience,
            [new Claim("idUser", idUser), new Claim("sub", sub), new Claim("idRole", "1"), new Claim("nameRole", role)],
            expires: expired ? DateTime.UtcNow.AddMinutes(-1) : DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)), SecurityAlgorithms.HmacSha256)));

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) connection.Dispose();
    }
}

