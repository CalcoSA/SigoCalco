using System.Text;
using Meetings.Api.Security;
using Meetings.Api.Responses;
using Microsoft.AspNetCore.Mvc;
using Meetings.Application.Interfaces;
using Meetings.Application.Services;
using Meetings.Infrastructure.Persistence.Extensions;
using Meetings.Infrastructure.Persistence.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;

var builder = WebApplication.CreateBuilder(args);
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
        .AddUserSecrets<Program>(optional: true).AddEnvironmentVariables();
}
builder.Services.AddControllers().ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = _ => new BadRequestObjectResult(
        ApiResponse.Failure("Solicitud inválida: revise los campos obligatorios, sus tipos y el formato JSON."));
    options.SuppressMapClientErrors = true;
});
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        In = ParameterLocation.Header, Description = "JWT emitido por SigoCalco.Authentication."
    });
    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {
        [new OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<MeetingsExceptionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<ITemplateApplication, TemplateApplication>();
builder.Services.AddScoped<IActaApplication, ActaApplication>();
builder.Services.AddSingleton(provider =>
{
    var options = provider.GetRequiredService<IConfiguration>().GetSection("Integrations:Calendar").Get<CalendarOptions>() ?? new();
    if (options.Mode is not ("Simulated" or "Graph")) throw new InvalidOperationException("Calendar:Mode debe ser Simulated o Graph.");
    return options;
});
builder.Services.AddHttpClient("GraphTransport", client => client.Timeout = TimeSpan.FromSeconds(20))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false, PooledConnectionLifetime = TimeSpan.FromMinutes(5) });
builder.Services.AddSingleton(provider => new GraphAccessTokenProvider(
    provider.GetRequiredService<IHttpClientFactory>().CreateClient("GraphTransport"), provider.GetRequiredService<CalendarOptions>()));
builder.Services.AddScoped<IMeetingCalendarProvider>(provider =>
{
    var options = provider.GetRequiredService<CalendarOptions>();
    return options.Mode == "Simulated" ? new SimulatedCalendarProvider() : new GraphCalendarProvider(
        provider.GetRequiredService<IHttpClientFactory>().CreateClient("GraphTransport"), provider.GetRequiredService<GraphAccessTokenProvider>(), options);
});
builder.Services.AddScoped<IMeetingsIdentity, MeetingsIdentityAccessor>();
builder.Services.AddSingleton<ISectionContentValidator, FormSectionValidator>();
builder.Services.AddSingleton<SectionJsonValidator>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
{
    var secret = builder.Configuration["Jwt:Secret"];
    var issuer = builder.Configuration["Jwt:Issuer"];
    var audience = builder.Configuration["Jwt:Audience"];
    if (string.IsNullOrWhiteSpace(secret) || Encoding.UTF8.GetByteCount(secret) < 32 ||
        string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(audience))
        throw new InvalidOperationException("Configure Jwt:Secret (mínimo 32 bytes), Jwt:Issuer y Jwt:Audience externamente.");
    options.MapInboundClaims = false;
    options.Events = new JwtBearerEvents
    {
        OnChallenge = async context =>
        {
            context.HandleResponse();
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.Headers.WWWAuthenticate = "Bearer";
            await context.Response.WriteAsJsonAsync(ApiResponse.Failure("Se requiere un JWT válido y vigente."));
        },
        OnForbidden = context =>
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return context.Response.WriteAsJsonAsync(ApiResponse.Failure("No tiene permiso para realizar esta operación."));
        }
    };
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true, ValidIssuer = issuer,
        ValidateAudience = true, ValidAudience = audience,
        ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
        ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.Zero, NameClaimType = "userLogin", RoleClaimType = "nameRole"
    };
});
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).ValidateOnStart();
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

var app = builder.Build();
// Explicit local operator recovery; no new HTTP endpoint or automatic sending on startup.
if (builder.Configuration["retry-meeting"] is { } retryId)
{
    if (!int.TryParse(retryId, out var meetingId) || meetingId <= 0) throw new InvalidOperationException("retry-meeting requiere un identificador positivo.");
    using var scope = app.Services.CreateScope();
    try
    {
        var result = await scope.ServiceProvider.GetRequiredService<IMeetingIntegrationService>().SynchronizeAsync(meetingId, CancellationToken.None);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(ApiResponse.Success(result), new System.Text.Json.JsonSerializerOptions(System.Text.Json.JsonSerializerDefaults.Web)));
        Environment.ExitCode = result.State == "ERROR" ? 1 : 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex is Meetings.Domain.Exceptions.MeetingsRequestException request ? request.Message : "No se pudo recuperar la integración. Revise configuración y disponibilidad de MySQL.");
        Environment.ExitCode = 1;
    }
    await app.DisposeAsync();
    return;
}
app.UseExceptionHandler();
app.UseStatusCodePages(async context =>
{
    var status = context.HttpContext.Response.StatusCode;
    var message = status switch
    {
        404 => "No se encontró el recurso solicitado.",
        405 => "El método HTTP no está permitido para este recurso.",
        415 => "Utilice el tipo de contenido application/json.",
        _ => "No se pudo completar la solicitud."
    };
    await context.HttpContext.Response.WriteAsJsonAsync(ApiResponse.Failure(message));
});
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.MapOpenApi().RequireAuthorization();
}
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }
