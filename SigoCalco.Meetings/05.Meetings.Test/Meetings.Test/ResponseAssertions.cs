using System.Net.Http.Json;
using Meetings.Api.Responses;

namespace Meetings.Test;

public static class ResponseAssertions
{
    public static async Task<T?> ReadResultAsync<T>(this HttpContent content)
    {
        var response = await content.ReadFromJsonAsync<ApiResponse<T>>();
        Assert.NotNull(response);
        Assert.True(response.IsSuccess);
        Assert.False(string.IsNullOrWhiteSpace(response.Message));
        Assert.NotNull(response.Result);
        return response.Result;
    }
    public static async Task<T?> GetResultAsync<T>(this HttpClient client, string url)
    {
        using var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadResultAsync<T>();
    }
}
