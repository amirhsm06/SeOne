using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeOne.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddManualPaymentSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ManualPaymentSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MaxReceiptSizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    AllowedMimeTypes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    WhatsAppContactNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ManualPaymentSettings", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ManualPaymentSettings");
        }
    }
}
