using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HelpDeskApi.Migrations;

public partial class AddResolvedClosedTimestamps : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "ResolvedAtUtc",
            table: "Tickets",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "ClosedAtUtc",
            table: "Tickets",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ResolvedAtUtc", table: "Tickets");
        migrationBuilder.DropColumn(name: "ClosedAtUtc", table: "Tickets");
    }
}