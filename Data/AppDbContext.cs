using HelpDeskApi.Domain;
using Microsoft.EntityFrameworkCore;

namespace HelpDeskApi.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Email único
        modelBuilder.Entity<User>()
            .HasIndex(u => u.Email)
            .IsUnique();

        // Ticket -> CreatedBy (muchos tickets pueden ser creados por un user)
        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.CreatedBy)
            .WithMany()
            .HasForeignKey(t => t.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);

        // Ticket -> AssignedTo (opcional)
        modelBuilder.Entity<Ticket>()
            .HasOne(t => t.AssignedTo)
            .WithMany()
            .HasForeignKey(t => t.AssignedToId)
            .OnDelete(DeleteBehavior.SetNull);

        // Comment -> Ticket
        modelBuilder.Entity<TicketComment>()
            .HasOne(c => c.Ticket)
            .WithMany()
            .HasForeignKey(c => c.TicketId);

        // Comment -> Author
        modelBuilder.Entity<TicketComment>()
            .HasOne(c => c.Author)
            .WithMany()
            .HasForeignKey(c => c.AuthorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}