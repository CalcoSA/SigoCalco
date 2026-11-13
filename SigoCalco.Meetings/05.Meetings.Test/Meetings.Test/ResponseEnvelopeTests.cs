using System.Net;
using System.Net.Http.Json;
using System.Text;
using Meetings.Application.Dtos;
using Meetings.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Meetings.Test;

public sealed class ResponseEnvelopeTests
{
    [Fact]
    public async Task Middleware_model_binding_routes_and_exceptions_share_error_contract()
    {
        using var factory = new MeetingsApiFactory(); using var client = factory.Client(); using var anonymous = factory.Client(false);
        await ActaApiTests.AssertFailure(await anonymous.GetAsync("/api/v1/acta-types"), HttpStatusCode.Unauthorized);
        await ActaApiTests.AssertFailure(await anonymous.PostAsJsonAsync("/api/v1/actas", new { }), HttpStatusCode.Unauthorized);
        await ActaApiTests.AssertFailure(await client.PostAsync("/api/v1/actas", new StringContent("{invalid", Encoding.UTF8, "application/json")), HttpStatusCode.BadRequest);
        await ActaApiTests.AssertFailure(await client.PostAsJsonAsync("/api/v1/actas", new { }), HttpStatusCode.BadRequest);
        await ActaApiTests.AssertFailure(await client.PostAsJsonAsync("/api/v1/meetings", new { actaId = 1 }), HttpStatusCode.NotFound);
        await ActaApiTests.AssertFailure(await client.GetAsync("/api/v1/actas"), HttpStatusCode.MethodNotAllowed);
        await ActaApiTests.AssertFailure(await client.PostAsJsonAsync("/api/v1/templates/999/versions", new CreateTemplateVersionDto([])), HttpStatusCode.NotFound);
        await ActaApiTests.AssertFailure(await client.GetAsync("/api/v1/templates?page=0"), HttpStatusCode.BadRequest);
    }
    [Fact]
    public async Task Unexpected_exception_returns_safe_500_without_internal_details()
    {
        using var factory = new MeetingsApiFactory { Customize = services =>
        {
            services.RemoveAll<IActaApplication>(); services.AddScoped<IActaApplication, ThrowingApplication>();
        } };
        using var client = factory.Client();
        var input = new CreateActaDto(1, "X", new(2026, 11, 11), "R01", [], [], null);
        using var response = await ActaApiTests.SendAsync(client, input);
        await ActaApiTests.AssertFailure(response, HttpStatusCode.InternalServerError);
        Assert.DoesNotContain("secret-detail", await response.Content.ReadAsStringAsync());
    }
    private sealed class ThrowingApplication : IActaApplication
    {
        public Task<CreatedActaDto> CreateAsync(CreateActaDto input, string? idempotencyKey, CancellationToken ct) =>
            throw new InvalidOperationException("secret-detail");
    }
}
