using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeOne.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddTeacherLedClassSlotsPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "TeacherId",
                table: "CourseClasses",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<string>(
                name: "Currency",
                table: "CourseClasses",
                type: "character varying(3)",
                maxLength: 3,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "FinalPrice",
                table: "CourseClasses",
                type: "numeric(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "ClassSlotTeacherAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassSlotId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassSlotTeacherAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassSlotTeacherAssignments_AspNetUsers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClassSlotTeacherAssignments_CourseClasses_ClassSlotId",
                        column: x => x.ClassSlotId,
                        principalTable: "CourseClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassSlotTeacherAssignments_ClassSlotId",
                table: "ClassSlotTeacherAssignments",
                column: "ClassSlotId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassSlotTeacherAssignments_TeacherId_Status",
                table: "ClassSlotTeacherAssignments",
                columns: new[] { "TeacherId", "Status" });

            migrationBuilder.Sql("UPDATE \"CourseClasses\" SET \"FinalPrice\" = \"BasePrice\", \"Currency\" = COALESCE((SELECT \"Currency\" FROM \"Course\" WHERE \"Course\".\"Id\" = \"CourseClasses\".\"CourseId\"), 'IRR');");
            migrationBuilder.Sql("INSERT INTO \"ClassSlotTeacherAssignments\" (\"Id\", \"ClassSlotId\", \"TeacherId\", \"Status\", \"CreatedAt\") SELECT gen_random_uuid(), \"Id\", \"TeacherId\", 'confirmed', \"CreatedAt\" FROM \"CourseClasses\" WHERE \"TeacherId\" IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassSlotTeacherAssignments");

            migrationBuilder.DropColumn(
                name: "Currency",
                table: "CourseClasses");

            migrationBuilder.DropColumn(
                name: "FinalPrice",
                table: "CourseClasses");

            migrationBuilder.AlterColumn<Guid>(
                name: "TeacherId",
                table: "CourseClasses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
