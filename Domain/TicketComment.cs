namespace HelpDeskApi.Domain;

public class TicketComment
{
    public int Id { get; set; }

    public int TicketId { get; set; }
    public Ticket? Ticket { get; set; }

    public int AuthorId { get; set; }
    public User? Author { get; set; }

    public required string Body { get; set; }
    public bool IsInternal { get; set; } = false;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}