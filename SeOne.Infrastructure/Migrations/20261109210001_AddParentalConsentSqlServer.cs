using Microsoft.EntityFrameworkCore.Migrations;
using SeOne.Infrastructure.Persistence;

#nullable disable

namespace SeOne.Infrastructure.Migrations;

public partial class AddParentalConsentSqlServer : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "ParentalConsent",
            table: "AspNetUsers",
            type: "bit",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ParentalConsent", table: "AspNetUsers");
    }
}
