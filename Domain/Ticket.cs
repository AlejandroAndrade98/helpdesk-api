namespace HelpDeskApi.Domain;

public class Ticket
{
    public int Id { get; set; }

    public required string Title { get; set; }
    public required string Description { get; set; }

    public TicketStatus Status { get; set; } = TicketStatus.New;
    public TicketPriority Priority { get; set; } = TicketPriority.Medium;

    // Relaciones con User
    public int CreatedById { get; set; }
    public User? CreatedBy { get; set; }

    public int? AssignedToId { get; set; }
    public User? AssignedTo { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public DateTime? ClosedAtUtc { get; set; }
}