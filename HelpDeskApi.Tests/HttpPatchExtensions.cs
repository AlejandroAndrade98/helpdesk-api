using System.Net.Http.Json;

namespace HelpDeskApi.Tests;

public static class HttpPatchExtensions
{
    public static Task<HttpResponseMessage> PatchAsJsonAsync<T>(this HttpClient client, string requestUri, T body)
    {
        var request = new HttpRequestMessage(HttpMethod.Patch, requestUri)
        {
            Content = JsonContent.Create(body)
        };
        return client.SendAsync(request);
    }
}