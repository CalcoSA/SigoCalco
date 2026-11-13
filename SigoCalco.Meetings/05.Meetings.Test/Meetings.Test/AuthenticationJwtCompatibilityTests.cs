using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using SigoCalco.Authentication.Application.Services;
using SigoCalco.Authentication.Domain.Dtos;

namespace Meetings.Test;

public sealed class AuthenticationJwtCompatibilityTests
{
    [Fact]
    public async Task Meetings_accepts_the_unchanged_Authentication_token_issuer()
    {
        using var factory = new MeetingsApiFactory();
        using var client = factory.Client(false);
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Jwt:Secret"] = MeetingsApiFactory.Secret,
            ["Jwt:Issuer"] = "meetings-tests", ["Jwt:Audience"] = "meetings-tests"
        }).Build();
        // Calls Authentication's linked source, not a reimplementation. No login/session writes.
        var issuer = new JwtTokenApplication(config);
        var token = issuer.GenerateToken(new UserDto
        {
            IdUser = 1, IdRole = 1, NameRole = "Administrador", UserLogin = "compatibility-test"
        }, Guid.NewGuid().ToString("D"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/acta-types")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/v1/templates")).StatusCode);
    }
}
