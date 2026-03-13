namespace HelpDeskApi.Domain;

public class User
{
    public int Id { get; set; }
    public required string DisplayName { get; set; }
    public required string Email { get; set; }

    public UserRole Role { get; set; } = UserRole.Requester;

    public string? PasswordHash { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}