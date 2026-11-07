using Microsoft.EntityFrameworkCore;
using Meetings.Infrastructure.Persistence.Extensions;
using Meetings.Application.Interfaces;
using Meetings.Application.Services;



var builder = WebApplication.CreateBuilder(args);

// Configuración de controladores
builder.Services.AddControllers();

// Documentación OpenAPI
builder.Services.AddOpenApi();

// Configuración de MySQL
var connectionString = builder.Configuration
    .GetConnectionString("MeetingsDatabase")
    ?? throw new InvalidOperationException(
        "No se configuró la conexión MeetingsDatabase.");

builder.Services.AddInfrastructure(connectionString);

// Servicios de aplicación
builder.Services.AddScoped<
    IMeetingApplication,
    MeetingApplication>();

// Autorización para endpoints protegidos
builder.Services.AddAuthorization();

builder.Services.AddScoped<
    IMinuteApplication,
    MinuteApplication>();

builder.Services.AddScoped<
    IAttendeeApplication,
    AttendeeApplication>();

builder.Services.AddScoped<
    IAttendeeAssignmentApplication,
    AttendeeAssignmentApplication>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();



app.Run();


app.Run();
