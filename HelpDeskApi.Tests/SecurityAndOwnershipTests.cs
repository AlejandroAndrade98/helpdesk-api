using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace HelpDeskApi.Tests;

public class SecurityAndOwnershipTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SecurityAndOwnershipTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Requester_cannot_view_other_requester_ticket_returns_404()
    {
        var client = _factory.CreateClient();

        var tokenA = await TestHelpers.RegisterAndLoginAsync(client, "Req A", "reqA@test.com", "Password123!", role: 1);
        var tokenB = await TestHelpers.RegisterAndLoginAsync(client, "Req B", "reqB@test.com", "Password123!", role: 1);

        var requesterA = _factory.CreateClient().WithBearer(tokenA);
        var requesterB = _factory.CreateClient().WithBearer(tokenB);

        var ticketId = await TestHelpers.CreateTicketAsync(requesterA, "Ticket A", "Desc A", priority: 2);

        var res = await requesterB.GetAsync($"/tickets/{ticketId}");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Requester_does_not_see_internal_comments_only_external()
    {
        var client = _factory.CreateClient();

        var tokenRequester = await TestHelpers.RegisterAndLoginAsync(client, "Req", "req@test.com", "Password123!", role: 1);
        var tokenAgent = await TestHelpers.RegisterAndLoginAsync(client, "Agent", "agent@test.com", "Password123!", role: 2);

        var requester = _factory.CreateClient().WithBearer(tokenRequester);
        var agent = _factory.CreateClient().WithBearer(tokenAgent);

        var ticketId = await TestHelpers.CreateTicketAsync(requester, "Ticket", "Desc", priority: 2);

        await TestHelpers.AddCommentAsync(agent, ticketId, "Internal note", isInternal: true);
        await TestHelpers.AddCommentAsync(agent, ticketId, "External note", isInternal: false);

        var res = await requester.GetAsync($"/tickets/{ticketId}/comments");
        res.EnsureSuccessStatusCode();

        var list = await res.Content.ReadFromJsonAsync<List<CommentItem>>();
        Assert.NotNull(list);
        Assert.Single(list);
        Assert.False(list![0].IsInternal);
        Assert.Equal("External note", list[0].Body);
    }

    [Fact]
    public async Task Requester_cannot_create_internal_comment_returns_403()
    {
        var client = _factory.CreateClient();

        var tokenRequester = await TestHelpers.RegisterAndLoginAsync(client, "Req", "req2@test.com", "Password123!", role: 1);
        var requester = _factory.CreateClient().WithBearer(tokenRequester);

        var ticketId = await TestHelpers.CreateTicketAsync(requester, "Ticket", "Desc", priority: 2);

        var res = await requester.PostAsJsonAsync($"/tickets/{ticketId}/comments", new
        {
            body = "Trying internal",
            isInternal = true
        });

        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Agent_can_see_internal_comments()
    {
        var client = _factory.CreateClient();

        var tokenRequester = await TestHelpers.RegisterAndLoginAsync(client, "Req", "req3@test.com", "Password123!", role: 1);
        var tokenAgent = await TestHelpers.RegisterAndLoginAsync(client, "Agent", "agent2@test.com", "Password123!", role: 2);

        var requester = _factory.CreateClient().WithBearer(tokenRequester);
        var agent = _factory.CreateClient().WithBearer(tokenAgent);

        var ticketId = await TestHelpers.CreateTicketAsync(requester, "Ticket", "Desc", priority: 2);

        await TestHelpers.AddCommentAsync(agent, ticketId, "Internal note", isInternal: true);
        await TestHelpers.AddCommentAsync(agent, ticketId, "External note", isInternal: false);

        var res = await agent.GetAsync($"/tickets/{ticketId}/comments");
        res.EnsureSuccessStatusCode();

        var list = await res.Content.ReadFromJsonAsync<List<CommentItem>>();
        Assert.NotNull(list);
        Assert.Equal(2, list!.Count);
        Assert.Contains(list, x => x.IsInternal);
        Assert.Contains(list, x => !x.IsInternal);
    }

    [Fact]
    public async Task Cannot_close_ticket_if_not_resolved_returns_409()
    {
        var client = _factory.CreateClient();

        var tokenRequester = await TestHelpers.RegisterAndLoginAsync(client, "Req", "req4@test.com", "Password123!", role: 1);
        var tokenAgent = await TestHelpers.RegisterAndLoginAsync(client, "Agent", "agent3@test.com", "Password123!", role: 2);

        var requester = _factory.CreateClient().WithBearer(tokenRequester);
        var agent = _factory.CreateClient().WithBearer(tokenAgent);

        var ticketId = await TestHelpers.CreateTicketAsync(requester, "Ticket", "Desc", priority: 2);

        var res = await agent.PatchAsJsonAsync($"/tickets/{ticketId}/status", new { status = 6 });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    public record CommentItem(int Id, string Body, bool IsInternal);
}