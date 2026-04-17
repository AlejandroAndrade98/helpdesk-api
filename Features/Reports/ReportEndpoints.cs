using HelpDeskApi.Data;
using HelpDeskApi.Domain;
using Microsoft.EntityFrameworkCore;
using System.Text;

namespace HelpDeskApi.Features.Reports;

public static class ReportEndpoints
{
    public static RouteGroupBuilder MapReportEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/reports")
            .RequireAuthorization(policy => policy.RequireRole("Admin"));

        // A. GET /reports/overview
        group.MapGet("/overview", async (
            DateTime? fromUtc,
            DateTime? toUtc,
            int? status,
            int? priority,
            int? assignedToId,
            AppDbContext db) =>
        {
            if (fromUtc > toUtc)
                return Results.BadRequest(new { message = "fromUtc no puede ser mayor que toUtc." });

            var baseQuery = ApplyTicketFilters(db.Tickets.AsNoTracking(), fromUtc, toUtc, status, priority, assignedToId);

            var total = await baseQuery.CountAsync();
            var newTickets = await baseQuery.CountAsync(t => t.Status == TicketStatus.New);
            var openTickets = await baseQuery.CountAsync(t => t.Status == TicketStatus.Open);
            var inProgressTickets = await baseQuery.CountAsync(t => t.Status == TicketStatus.InProgress);
            var waitingOnCustomerTickets = await baseQuery.CountAsync(t => t.Status == TicketStatus.WaitingOnCustomer);
            var resolvedTickets = await baseQuery.CountAsync(t => t.Status == TicketStatus.Resolved);
            var closedTickets = await baseQuery.CountAsync(t => t.Status == TicketStatus.Closed);
            var onHoldTickets = await baseQuery.CountAsync(t => t.Status == TicketStatus.OnHold);
            var assignedTickets = await baseQuery.CountAsync(t => t.AssignedToId != null);
            var unassignedTickets = await baseQuery.CountAsync(t => t.AssignedToId == null);
            var criticalTickets = await baseQuery.CountAsync(t => t.Priority == TicketPriority.Critical);
            var highPriorityTickets = await baseQuery.CountAsync(t => t.Priority == TicketPriority.High);

            return Results.Ok(new ReportOverviewResponse(
                total,
                newTickets,
                openTickets,
                inProgressTickets,
                waitingOnCustomerTickets,
                resolvedTickets,
                closedTickets,
                onHoldTickets,
                assignedTickets,
                unassignedTickets,
                criticalTickets,
                highPriorityTickets
            ));
        });

        // B. GET /reports/tickets-by-status
        group.MapGet("/tickets-by-status", async (
            DateTime? fromUtc,
            DateTime? toUtc,
            int? priority,
            int? assignedToId,
            AppDbContext db) =>
        {
            if (fromUtc > toUtc)
                return Results.BadRequest(new { message = "fromUtc no puede ser mayor que toUtc." });

            var query = ApplyTicketFilters(db.Tickets.AsNoTracking(), fromUtc, toUtc, null, priority, assignedToId);

            var items = await query
                .GroupBy(t => t.Status)
                .Select(g => new StatusCount((int)g.Key, g.Count()))
                .OrderBy(x => x.Status)
                .ToListAsync();

            return Results.Ok(items);
        });

        // C. GET /reports/tickets-by-priority
        group.MapGet("/tickets-by-priority", async (
            DateTime? fromUtc,
            DateTime? toUtc,
            int? status,
            int? assignedToId,
            AppDbContext db) =>
        {
            if (fromUtc > toUtc)
                return Results.BadRequest(new { message = "fromUtc no puede ser mayor que toUtc." });

            var query = ApplyTicketFilters(db.Tickets.AsNoTracking(), fromUtc, toUtc, status, null, assignedToId);

            var items = await query
                .GroupBy(t => t.Priority)
                .Select(g => new PriorityCount((int)g.Key, g.Count()))
                .OrderBy(x => x.Priority)
                .ToListAsync();

            return Results.Ok(items);
        });

        // D. GET /reports/tickets-trend
        group.MapGet("/tickets-trend", async (
            DateTime? fromUtc,
            DateTime? toUtc,
            int? status,
            int? priority,
            int? assignedToId,
            AppDbContext db) =>
        {
            if (fromUtc > toUtc)
                return Results.BadRequest(new { message = "fromUtc no puede ser mayor que toUtc." });

            var query = ApplyTicketFilters(db.Tickets.AsNoTracking(), fromUtc, toUtc, status, priority, assignedToId);

            // Materializar en memoria para evitar problemas de traducción EF Core -> SQL
            var grouped = await query
                .GroupBy(t => t.CreatedAtUtc.Date)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToListAsync();

            var items = grouped
                .Select(g => new TrendEntry(g.Key.ToString("yyyy-MM-dd"), g.Count))
                .OrderBy(x => x.Date)
                .ToList();

            return Results.Ok(items);
        });

        // E. GET /reports/agent-workload
        group.MapGet("/agent-workload", async (
            DateTime? fromUtc,
            DateTime? toUtc,
            int? status,
            int? priority,
            int? assignedToId,
            AppDbContext db) =>
        {
            if (fromUtc > toUtc)
                return Results.BadRequest(new { message = "fromUtc no puede ser mayor que toUtc." });

            var agents = await db.Users
                .AsNoTracking()
                .Where(u => u.Role == UserRole.Agent || u.Role == UserRole.Admin)
                .ToListAsync();

            var ticketsQuery = ApplyTicketFilters(db.Tickets.AsNoTracking(), fromUtc, toUtc, status, priority, assignedToId);

            var ticketCounts = await ticketsQuery
                .GroupBy(t => t.AssignedToId)
                .Select(g => new
                {
                    AssignedToId = g.Key,
                    AssignedCount = g.Count(),
                    ActiveCount = g.Count(t =>
                        t.Status == TicketStatus.New ||
                        t.Status == TicketStatus.Open ||
                        t.Status == TicketStatus.InProgress ||
                        t.Status == TicketStatus.WaitingOnCustomer ||
                        t.Status == TicketStatus.OnHold),
                    ResolvedCount = g.Count(t => t.Status == TicketStatus.Resolved),
                    ClosedCount = g.Count(t => t.Status == TicketStatus.Closed)
                })
                .ToDictionaryAsync(x => x.AssignedToId ?? 0);

            var items = agents.Select(u =>
            {
                var counts = ticketCounts.TryGetValue(u.Id, out var c) ? c : null;
                return new AgentWorkloadEntry(
                    u.Id,
                    u.DisplayName,
                    (int)u.Role,
                    counts?.AssignedCount ?? 0,
                    counts?.ActiveCount ?? 0,
                    counts?.ResolvedCount ?? 0,
                    counts?.ClosedCount ?? 0
                );
            })
            .OrderByDescending(x => x.ActiveTickets)
            .ThenByDescending(x => x.AssignedTickets)
            .ThenBy(x => x.DisplayName)
            .ToList();

            return Results.Ok(items);
        });

        // F. GET /reports/export/tickets
        group.MapGet("/export/tickets", async (
            DateTime? fromUtc,
            DateTime? toUtc,
            int? status,
            int? priority,
            int? assignedToId,
            AppDbContext db) =>
        {
            if (fromUtc > toUtc)
                return Results.BadRequest(new { message = "fromUtc no puede ser mayor que toUtc." });

            // Materializar para transformar en memoria (nombres legibles de status/priority)
            var tickets = await ApplyTicketFilters(db.Tickets.AsNoTracking(), fromUtc, toUtc, status, priority, assignedToId)
                .Select(t => new
                {
                    t.Id,
                    t.Title,
                    t.Status,
                    t.Priority,
                    CreatedByName = t.CreatedBy!.DisplayName,
                    AssignedToName = t.AssignedTo != null ? t.AssignedTo.DisplayName : "",
                    t.CreatedAtUtc,
                    t.UpdatedAtUtc
                })
                .ToListAsync();

            var rows = tickets.Select(t => new TicketExportRow(
                t.Id,
                t.Title,
                t.Status.ToString(),
                t.Priority.ToString(),
                t.CreatedByName,
                t.AssignedToName,
                t.CreatedAtUtc,
                t.UpdatedAtUtc
            )).ToList();

            var csv = BuildCsv(rows);
            var fileName = $"tickets-export-{DateTime.UtcNow:yyyyMMddHHmmss}.csv";

            return Results.File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
        });

        return group;
    }

    private static IQueryable<Ticket> ApplyTicketFilters(
        IQueryable<Ticket> query,
        DateTime? fromUtc,
        DateTime? toUtc,
        int? status,
        int? priority,
        int? assignedToId)
    {
        if (fromUtc is not null)
            query = query.Where(t => t.CreatedAtUtc >= fromUtc.Value);

        if (toUtc is not null)
            query = query.Where(t => t.CreatedAtUtc <= toUtc.Value);

        if (status is not null)
            query = query.Where(t => (int)t.Status == status);

        if (priority is not null)
            query = query.Where(t => (int)t.Priority == priority);

        if (assignedToId is not null)
            query = query.Where(t => t.AssignedToId == assignedToId);

        return query;
    }

    private static string BuildCsv(List<TicketExportRow> rows)
    {
        var sb = new StringBuilder();

        sb.AppendLine("Id,Title,Status,Priority,CreatedByName,AssignedToName,CreatedAtUtc,UpdatedAtUtc");

        foreach (var row in rows)
        {
            sb.AppendLine($"{row.Id},{EscapeCsv(row.Title)},{EscapeCsv(row.Status)},{EscapeCsv(row.Priority)},{EscapeCsv(row.CreatedByName)},{EscapeCsv(row.AssignedToName)},{row.CreatedAtUtc:yyyy-MM-ddTHH:mm:ssZ},{row.UpdatedAtUtc:yyyy-MM-ddTHH:mm:ssZ}");
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return $"\"{value.Replace("\"", "\"\"")}\"";
        return value;
    }
}

// Response records

public record ReportOverviewResponse(
    int TotalTickets,
    int NewTickets,
    int OpenTickets,
    int InProgressTickets,
    int WaitingOnCustomerTickets,
    int ResolvedTickets,
    int ClosedTickets,
    int OnHoldTickets,
    int AssignedTickets,
    int UnassignedTickets,
    int CriticalTickets,
    int HighPriorityTickets
);

public record StatusCount(int Status, int Count);
public record PriorityCount(int Priority, int Count);
public record TrendEntry(string Date, int Count);

public record AgentWorkloadEntry(
    int UserId,
    string DisplayName,
    int Role,
    int AssignedTickets,
    int ActiveTickets,
    int ResolvedTickets,
    int ClosedTickets
);

public record TicketExportRow(
    int Id,
    string Title,
    string Status,
    string Priority,
    string CreatedByName,
    string AssignedToName,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc
);