using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace HelpDeskApi.Tests;

public class PrivacyAndRolesTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public PrivacyAndRolesTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Requester_ticket_list_hides_emails_createdBy_and_assignedTo()
    {
        // Arrange
        var client = _factory.CreateClient();

        var requesterToken = await TestHelpers.RegisterAndLoginAsync(
            client, "Requester", "privacy_req@test.com", "Password123!", role: 1);

        var agentToken = await TestHelpers.RegisterAndLoginAsync(
            client, "Agent", "privacy_agent@test.com", "Password123!", role: 2);

        var requester = _factory.CreateClient().WithBearer(requesterToken);
        var agent = _factory.CreateClient().WithBearer(agentToken);

        // Requester crea ticket (no asignado)
        var ticketId = await TestHelpers.CreateTicketAsync(
            requester, "Privacy Ticket", "Desc", priority: 2);

        // Agent asigna el ticket a sí mismo (Agent/Admin only)
        var assignRes = await agent.PatchAsJsonAsync($"/tickets/{ticketId}/assign", new { assignedToId = 2 });
        // Nota: el ID del agente debería ser 2 en este escenario (primer requester = 1, agente = 2)
        // si alguna vez cambia el orden, lo hacemos dinámico luego.
        assignRes.EnsureSuccessStatusCode();

        // Act
        var res = await requester.GetAsync("/tickets?page=1&pageSize=10");
        res.EnsureSuccessStatusCode();

        var payload = await res.Content.ReadFromJsonAsync<TicketsListResponse>();
        Assert.NotNull(payload);
        Assert.True(payload!.Items.Count >= 1);

        var item = payload.Items[0];

        // Assert: requester NO ve emails
        Assert.Null(item.CreatedBy.Email);
        if (item.AssignedTo is not null)
            Assert.Null(item.AssignedTo.Email);
    }

    public record TicketsListResponse(int Page, int PageSize, int Total, List<TicketListItem> Items);
    public record TicketListItem(int Id, string Title, int Status, int Priority, UserMini CreatedBy, UserMini? AssignedTo, DateTime CreatedAtUtc);
    public record UserMini(int Id, string DisplayName, string? Email);


    [Fact]
    public async Task Users_list_is_admin_only()
    {
        var client = _factory.CreateClient();

        var requesterToken = await TestHelpers.RegisterAndLoginAsync(
            client, "Req", "rbac_req@test.com", "Password123!", role: 1);

        var agentToken = await TestHelpers.RegisterAndLoginAsync(
            client, "Agent", "rbac_agent@test.com", "Password123!", role: 2);

        var adminToken = await TestHelpers.RegisterAndLoginAsync(
            client, "Admin", "rbac_admin@test.com", "Password123!", role: 3);

        var requester = _factory.CreateClient().WithBearer(requesterToken);
        var agent = _factory.CreateClient().WithBearer(agentToken);
        var admin = _factory.CreateClient().WithBearer(adminToken);

        var resReq = await requester.GetAsync("/users");
        Assert.Equal(HttpStatusCode.Forbidden, resReq.StatusCode);

        var resAgent = await agent.GetAsync("/users");
        Assert.Equal(HttpStatusCode.Forbidden, resAgent.StatusCode);

        var resAdmin = await admin.GetAsync("/users");
        Assert.Equal(HttpStatusCode.OK, resAdmin.StatusCode);
    }
}

