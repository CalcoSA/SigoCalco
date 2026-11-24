using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Meetings.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SigoCalco.Authentication.Application.Services;
using SigoCalco.Authentication.Domain.Dtos;

namespace Meetings.Test;

public sealed class CorsTests
{
    private const string ReactOrigin = "http://localhost:5173";
    private const string CompatibilityOrigin = "http://localhost:4200";
    private const string RequestedHeaders = "authorization,content-type,idempotency-key";

    private static MeetingsApiFactory Development() => new() { EnvironmentName = "Development" };
    private static HttpRequestMessage Preflight(string origin, string method = "POST", string path = "/api/v1/actas")
    {
        var request = new HttpRequestMessage(HttpMethod.Options, path);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", RequestedHeaders);
        return request;
    }
    private static void AssertAllowed(HttpResponseMessage response, string origin = ReactOrigin)
    {
        Assert.Equal(origin, Assert.Single(response.Headers.GetValues("Access-Control-Allow-Origin")));
        Assert.False(response.Headers.Contains("Access-Control-Allow-Credentials"));
    }
    private static void AssertNoCors(HttpResponseMessage response) =>
        Assert.DoesNotContain(response.Headers, h => h.Key.StartsWith("Access-Control-Allow-", StringComparison.OrdinalIgnoreCase));

    [Theory]
    [InlineData("GET", "/api/v1/actas/shared-with-me")]
    [InlineData("POST", "/api/v1/actas")]
    [InlineData("PUT", "/api/v1/actas/1")]
    [InlineData("PUT", "/api/v1/actas/1/participants")]
    [InlineData("PATCH", "/api/v1/actas/1/status")]
    [InlineData("GET", "/api/v1/templates")]
    [InlineData("POST", "/api/v1/templates/with-version")]
    [InlineData("POST", "/api/v1/permissions/ACTAS_GESTION/roles")]
    [InlineData("PUT", "/api/v1/permissions/ACTAS_GESTION")]
    public async Task Development_preflight_allows_methods_and_bearer_json_idempotency_headers_without_jwt(string method, string path)
    {
        using var factory = Development(); using var client = factory.Client(false);
        foreach (var origin in new[] { ReactOrigin, CompatibilityOrigin })
        {
            using var request = Preflight(origin, method, path);
            using var response = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); AssertAllowed(response, origin);
            Assert.Contains(method, response.Headers.GetValues("Access-Control-Allow-Methods").SelectMany(v => v.Split(',')).Select(v => v.Trim()));
            var headers = response.Headers.GetValues("Access-Control-Allow-Headers").SelectMany(v => v.Split(',')).Select(v => v.Trim().ToLowerInvariant());
            Assert.All(RequestedHeaders.Split(','), h => Assert.Contains(h, headers));
            Assert.Empty(await response.Content.ReadAsStringAsync());
        }
    }

    [Theory]
    [InlineData("http://localhost:5174")]
    [InlineData("http://localhost:3000")]
    [InlineData("http://127.0.0.1:5173")]
    [InlineData("https://localhost:5173")]
    [InlineData("http://localhost:5173.attacker.invalid")]
    [InlineData("https://not-allowed.invalid")]
    [InlineData("null")]
    public async Task Unlisted_origins_receive_no_cors_permission_on_preflight_or_actual_response(string origin)
    {
        using var factory = Development(); using var client = factory.Client();
        using var options = Preflight(origin);
        using var preflight = await client.SendAsync(options);
        Assert.Equal(HttpStatusCode.NoContent, preflight.StatusCode); AssertNoCors(preflight);
        client.DefaultRequestHeaders.Add("Origin", origin);
        using var actual = await client.GetAsync("/api/v1/acta-types");
        Assert.Equal(HttpStatusCode.OK, actual.StatusCode); AssertNoCors(actual);
        // CORS controls browser access; it does not replace JWT or reject non-browser clients.
    }

    [Theory]
    [InlineData("missing")] [InlineData("invalid")] [InlineData("expired")]
    [InlineData("issuer")] [InlineData("audience")]
    public async Task Unauthorized_responses_keep_the_standard_body_and_cors_headers(string tokenMode)
    {
        using var factory = Development(); using var client = factory.Client(false);
        client.DefaultRequestHeaders.Add("Origin", ReactOrigin);
        if (tokenMode != "missing") client.DefaultRequestHeaders.Authorization = new("Bearer", tokenMode switch
        {
            "invalid" => "invalid.token",
            "expired" => MeetingsApiFactory.Token(expired: true),
            "issuer" => MeetingsApiFactory.Token(issuer: "untrusted"),
            _ => MeetingsApiFactory.Token(audience: "untrusted")
        });
        using var response = await client.GetAsync("/api/v1/acta-types");
        await ActaApiTests.AssertFailure(response, HttpStatusCode.Unauthorized); AssertAllowed(response);
        Assert.Equal("Bearer", Assert.Single(response.Headers.WwwAuthenticate).Scheme);
    }

    [Fact]
    public async Task Functional_and_administrative_forbidden_responses_keep_cors_headers()
    {
        using var factory = new MeetingsApiFactory { EnvironmentName = "Development", Customize = services =>
        {
            services.RemoveAll<IAuthenticationDirectory>();
            services.AddScoped<IAuthenticationDirectory, SharedActasTests.Directory>();
        } };
        using var client = factory.Client(); client.DefaultRequestHeaders.Add("Origin", ReactOrigin);
        using var functional = await client.GetAsync("/api/v1/actas"); // Exception handler: ACTAS_GESTION is empty in the isolated fixture.
        await ActaApiTests.AssertFailure(functional, HttpStatusCode.Forbidden); AssertAllowed(functional);
        SharedActasTests.Authenticate(client, 2);
        using var administrative = await client.GetAsync("/api/v1/permissions"); // JWT OnForbidden authorization policy.
        await ActaApiTests.AssertFailure(administrative, HttpStatusCode.Forbidden); AssertAllowed(administrative);
    }

    [Fact]
    public async Task Authentication_issuer_token_and_acta_post_keep_working_with_origin_and_idempotency_key()
    {
        using var factory = Development(); using var client = factory.Client(false);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = MeetingsApiFactory.Secret, ["Jwt:Issuer"] = "meetings-tests", ["Jwt:Audience"] = "meetings-tests"
        }).Build();
        var issued = new JwtTokenApplication(configuration).GenerateToken(new UserDto
        { IdUser = 1, IdRole = 1, NameRole = "Administrador", UserLogin = "test.admin" }, Guid.NewGuid().ToString("D"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued.Token);
        client.DefaultRequestHeaders.Add("Origin", ReactOrigin);
        using var get = await client.GetAsync("/api/v1/templates");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode); AssertAllowed(get);
        var input = await ActaApiTests.PrepareAsync(factory, client);
        using var post = await ActaApiTests.SendAsync(client, input);
        Assert.Equal(HttpStatusCode.Created, post.StatusCode); AssertAllowed(post);
        using var body = JsonDocument.Parse(await post.Content.ReadAsStringAsync());
        Assert.True(body.RootElement.GetProperty("isSuccess").GetBoolean());
        Assert.Equal("SIMULADO", body.RootElement.GetProperty("result").GetProperty("notifications").GetProperty("state").GetString());
    }

    [Fact]
    public async Task No_origin_requests_and_development_swagger_are_unchanged()
    {
        using var factory = Development(); using var client = factory.Client();
        using var actual = await client.GetAsync("/api/v1/acta-types");
        Assert.Equal(HttpStatusCode.OK, actual.StatusCode); AssertNoCors(actual);
        client.DefaultRequestHeaders.Authorization = null;
        using var ui = await client.GetAsync("/swagger/index.html");
        Assert.Equal(HttpStatusCode.OK, ui.StatusCode);
        using var swagger = await client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, swagger.StatusCode);
        using var document = JsonDocument.Parse(await swagger.Content.ReadAsStringAsync());
        Assert.True(document.RootElement.GetProperty("paths").TryGetProperty("/api/v1/actas/shared-with-me", out _));
        Assert.True(document.RootElement.GetProperty("components").GetProperty("securitySchemes").TryGetProperty("Bearer", out _));
    }

    [Theory]
    [InlineData("Production")] [InlineData("Staging")]
    public async Task Environments_without_an_explicit_list_do_not_inherit_development_origin(string environment)
    {
        using var factory = new MeetingsApiFactory { EnvironmentName = environment }; using var client = factory.Client();
        foreach (var origin in new[] { ReactOrigin, CompatibilityOrigin })
        {
            using var options = Preflight(origin); using var response = await client.SendAsync(options);
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); AssertNoCors(response);
            using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/acta-types");
            request.Headers.Add("Origin", origin);
            using var actual = await client.SendAsync(request);
            Assert.Equal(HttpStatusCode.OK, actual.StatusCode); AssertNoCors(actual);
        }
    }

    [Fact]
    public async Task An_explicit_external_configuration_is_used_without_a_code_change()
    {
        const string fixtureOrigin = "https://cors-fixture.invalid"; // Test only, never a deployed QA/production domain.
        using var factory = new MeetingsApiFactory { EnvironmentName = "Production", ConfigurationOverrides =
            new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = fixtureOrigin } };
        using var client = factory.Client(false);
        using var options = Preflight(fixtureOrigin); using var response = await client.SendAsync(options);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode); AssertAllowed(response, fixtureOrigin);
        using var devOptions = Preflight(ReactOrigin); using var devResponse = await client.SendAsync(devOptions);
        AssertNoCors(devResponse);
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://*.invalid")]
    [InlineData("null")]
    [InlineData("http://localhost:4200/")]
    [InlineData("http://localhost:4200/path")]
    [InlineData("http://localhost:4200?query=1")]
    [InlineData("http://example:password@localhost:4200")]
    public void Invalid_origin_configuration_fails_before_serving_requests(string origin)
    {
        using var factory = new MeetingsApiFactory { ConfigurationOverrides =
            new Dictionary<string, string?> { ["Cors:AllowedOrigins:0"] = origin } };
        var error = Assert.Throws<InvalidOperationException>(() => factory.Client());
        Assert.Contains("Cors:AllowedOrigins", error.Message);
        Assert.DoesNotContain(origin, error.Message);
    }
}
