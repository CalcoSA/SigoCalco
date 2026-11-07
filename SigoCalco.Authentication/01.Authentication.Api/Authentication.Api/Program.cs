using SigoCalco.Authentication.Application;
using SigoCalco.Authentication.Infrastructure.Persistence.Data;
using SigoCalco.Authentication.Api.Extensions;
using SessionOptions = SigoCalco.Authentication.Domain.Options.SessionOptions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

const string CorsPolicy = "SigoCalcoCorsPolicy";

builder.Services.AddControllers();
builder.Services.AddPersistence(builder.Configuration);
builder.Services.AddApplication();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<SessionOptions>()
    .Bind(builder.Configuration.GetSection("Session"))
    .Validate(options => options.IdleTimeoutMinutes > 0, "Session:IdleTimeoutMinutes debe ser mayor que cero.")
    .ValidateOnStart();
builder.Services.AddScoped<SessionJwtBearerEvents>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{

    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {

        Name = "Authorization",

        Type = SecuritySchemeType.Http,

        Scheme = "bearer",

        BearerFormat = "JWT",

        In = ParameterLocation.Header,

        Description = "Ingrese el token JWT."
    });


    options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
    {

        [new OpenApiSecuritySchemeReference("Bearer", document)] = []

    });

});

var jwtSecret = builder.Configuration["Jwt:Secret"] ?? throw new InvalidOperationException("No se encontró Jwt:Secret.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("No se encontró Jwt:Issuer.");
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? throw new InvalidOperationException("No se encontró Jwt:Audience.");
if (string.IsNullOrWhiteSpace(jwtSecret) || Encoding.UTF8.GetByteCount(jwtSecret) < 32)
    throw new InvalidOperationException("Configure Jwt:Secret con al menos 32 bytes mediante configuración externa.");
var jwtKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret));

builder.Services

    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)

    .AddJwtBearer(options =>

    {

        options.MapInboundClaims = false;
        options.EventsType = typeof(SessionJwtBearerEvents);
        options.TokenValidationParameters = new TokenValidationParameters
        {

            ValidateIssuer = true,

            ValidIssuer = jwtIssuer,

            ValidateAudience = true,

            ValidAudience = jwtAudience,

            ValidateIssuerSigningKey = true,

            IssuerSigningKey = jwtKey,

            ValidateLifetime = true,

            ClockSkew = TimeSpan.Zero

        };

    });

builder.Services.AddAuthorization();

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>() ?? [];

builder.Services.AddCors(options =>
{
    options.AddPolicy(CorsPolicy, policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors(CorsPolicy);

app.UseAuthentication();

app.UseAuthorization();

app.MapControllers();

app.Run();
