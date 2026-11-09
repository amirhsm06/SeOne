using Microsoft.EntityFrameworkCore.Migrations;
using SeOne.Infrastructure.Persistence;

#nullable disable

namespace SeOne.Migrations.Postgres.Migrations;

public partial class AddParentalConsentPostgres : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "ParentalConsent",
            table: "AspNetUsers",
            type: "boolean",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ParentalConsent", table: "AspNetUsers");
    }
}
