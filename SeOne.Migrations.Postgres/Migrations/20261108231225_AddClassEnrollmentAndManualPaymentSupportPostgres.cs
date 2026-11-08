using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeOne.Migrations.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddClassEnrollmentAndManualPaymentSupportPostgres : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClassGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClassId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    GroupType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassGroups_CourseClasses_ClassId",
                        column: x => x.ClassId,
                        principalTable: "CourseClasses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeacherGroupChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    IsAnnouncement = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeacherGroupChatMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClassGroupMembers",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Role = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    JoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassGroupMembers", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_ClassGroupMembers_ClassGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ClassGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClassGroupMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderRole = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Content = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClassGroupMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClassGroupMessages_ClassGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ClassGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClassGroupMembers_UserId_IsActive",
                table: "ClassGroupMembers",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassGroupMessages_GroupId_CreatedAt_Id",
                table: "ClassGroupMessages",
                columns: new[] { "GroupId", "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ClassGroupMessages_SenderId",
                table: "ClassGroupMessages",
                column: "SenderId");

            migrationBuilder.CreateIndex(
                name: "IX_ClassGroups_ClassId",
                table: "ClassGroups",
                column: "ClassId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClassGroups_IsActive",
                table: "ClassGroups",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_TeacherGroupChatMessages_CreatedAt_Id",
                table: "TeacherGroupChatMessages",
                columns: new[] { "CreatedAt", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_TeacherGroupChatMessages_SenderId",
                table: "TeacherGroupChatMessages",
                column: "SenderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClassGroupMembers");

            migrationBuilder.DropTable(
                name: "ClassGroupMessages");

            migrationBuilder.DropTable(
                name: "TeacherGroupChatMessages");

            migrationBuilder.DropTable(
                name: "ClassGroups");
        }
    }
}
