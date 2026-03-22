using System.Net.Http.Headers;
using System.Net.Http.Json;

namespace HelpDeskApi.Tests;

public static class TestHelpers
{
    public static async Task<string> RegisterAndLoginAsync(
        HttpClient client,
        string displayName,
        string email,
        string password,
        int role)
    {
        // Register
        var registerRes = await client.PostAsJsonAsync("/auth/register", new
        {
            displayName,
            email,
            password,
            role
        });

        // Puede ser 201 o 409 si repites email, en tests normalmente será 201
        if (!registerRes.IsSuccessStatusCode && registerRes.StatusCode.ToString() != "Conflict")
            registerRes.EnsureSuccessStatusCode();

        // Login
        var loginRes = await client.PostAsJsonAsync("/auth/login", new
        {
            email,
            password
        });

        loginRes.EnsureSuccessStatusCode();

        var payload = await loginRes.Content.ReadFromJsonAsync<LoginResponse>();
        return payload!.AccessToken;
    }

    public static HttpClient WithBearer(this HttpClient client, string token)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    public static async Task<int> CreateTicketAsync(HttpClient authedClient, string title, string description, int priority, int? assignedToId = null)
    {
        var res = await authedClient.PostAsJsonAsync("/tickets", new
        {
            title,
            description,
            priority,
            assignedToId
        });

        res.EnsureSuccessStatusCode();

        var payload = await res.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    public static async Task<int> AddCommentAsync(HttpClient authedClient, int ticketId, string body, bool isInternal)
    {
        var res = await authedClient.PostAsJsonAsync($"/tickets/{ticketId}/comments", new
        {
            body,
            isInternal
        });

        res.EnsureSuccessStatusCode();

        var payload = await res.Content.ReadFromJsonAsync<IdResponse>();
        return payload!.Id;
    }

    public record LoginResponse(string AccessToken);
    public record IdResponse(int Id);
}