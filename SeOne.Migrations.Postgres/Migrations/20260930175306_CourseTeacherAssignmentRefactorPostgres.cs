using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeOne.Migrations.Postgres.Migrations
{
    public partial class CourseTeacherAssignmentRefactorPostgres : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------
            // 1. Create the new tables while the old Course.TeacherId
            //    still exists so we can migrate the existing assignments.
            // ---------------------------------------------------------

            migrationBuilder.CreateTable(
                name: "CourseInstance",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseInstance", x => x.Id);

                    table.ForeignKey(
                        name: "FK_CourseInstance_Course_CourseId",
                        column: x => x.CourseId,
                        principalTable: "Course",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CourseInstanceTeacher",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseInstanceId = table.Column<Guid>(type: "uuid", nullable: false),
                    TeacherId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CourseInstanceTeacher", x => x.Id);

                    table.ForeignKey(
                        name: "FK_CourseInstanceTeacher_AspNetUsers_TeacherId",
                        column: x => x.TeacherId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);

                    table.ForeignKey(
                        name: "FK_CourseInstanceTeacher_CourseInstance_CourseInstanceId",
                        column: x => x.CourseInstanceId,
                        principalTable: "CourseInstance",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CourseInstance_CourseId",
                table: "CourseInstance",
                column: "CourseId");

            migrationBuilder.CreateIndex(
                name: "IX_CourseInstanceTeacher_CourseInstanceId_TeacherId",
                table: "CourseInstanceTeacher",
                columns: new[] { "CourseInstanceId", "TeacherId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CourseInstanceTeacher_TeacherId",
                table: "CourseInstanceTeacher",
                column: "TeacherId");

            // ---------------------------------------------------------
            // 2. Create one CourseInstance for every existing Course.
            //
            //    We preserve the Course.CreatedAt timestamp.
            // ---------------------------------------------------------

            migrationBuilder.Sql(
                """
                INSERT INTO "CourseInstance"
                    ("Id", "CourseId", "StartDate", "EndDate", "CreatedAt")
                SELECT
                    gen_random_uuid(),
                    c."Id",
                    NULL,
                    NULL,
                    c."CreatedAt"
                FROM "Course" c;
                """);

            // ---------------------------------------------------------
            // 3. Move the old Course.TeacherId assignment into
            //    CourseInstanceTeacher.
            // ---------------------------------------------------------

            migrationBuilder.Sql(
                """
                INSERT INTO "CourseInstanceTeacher"
                    ("Id", "CourseInstanceId", "TeacherId", "CreatedAt")
                SELECT
                    gen_random_uuid(),
                    ci."Id",
                    c."TeacherId",
                    c."CreatedAt"
                FROM "Course" c
                INNER JOIN "CourseInstance" ci
                    ON ci."CourseId" = c."Id";
                """);

            // ---------------------------------------------------------
            // 4. Only now remove the old Course.TeacherId relationship.
            // ---------------------------------------------------------

            migrationBuilder.DropForeignKey(
                name: "FK_Course_AspNetUsers_TeacherId",
                table: "Course");

            migrationBuilder.DropIndex(
                name: "IX_Course_TeacherId",
                table: "Course");

            migrationBuilder.DropColumn(
                name: "TeacherId",
                table: "Course");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // ---------------------------------------------------------
            // Restore TeacherId temporarily as nullable so existing
            // CourseInstanceTeacher assignments can be copied back.
            // ---------------------------------------------------------

            migrationBuilder.AddColumn<Guid>(
                name: "TeacherId",
                table: "Course",
                type: "uuid",
                nullable: true);

            // Choose the first teacher assigned to each course.
            migrationBuilder.Sql(
                """
                WITH ranked_teachers AS
                (
                    SELECT
                        ci."CourseId",
                        cit."TeacherId",
                        ROW_NUMBER() OVER
                        (
                            PARTITION BY ci."CourseId"
                            ORDER BY
                                ci."CreatedAt",
                                cit."CreatedAt",
                                cit."TeacherId"
                        ) AS rn
                    FROM "CourseInstance" ci
                    INNER JOIN "CourseInstanceTeacher" cit
                        ON cit."CourseInstanceId" = ci."Id"
                )
                UPDATE "Course" c
                SET "TeacherId" = r."TeacherId"
                FROM ranked_teachers r
                WHERE
                    r."CourseId" = c."Id"
                    AND r.rn = 1;
                """);

            // The old schema requires every Course to have a TeacherId.
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF EXISTS
                    (
                        SELECT 1
                        FROM "Course"
                        WHERE "TeacherId" IS NULL
                    )
                    THEN
                        RAISE EXCEPTION
                            'Cannot revert migration: one or more courses have no assigned teacher.';
                    END IF;
                END
                $$;
                """);

            migrationBuilder.Sql(
                """
                ALTER TABLE "Course"
                ALTER COLUMN "TeacherId" SET NOT NULL;
                """);

            migrationBuilder.DropTable(
                name: "CourseInstanceTeacher");

            migrationBuilder.DropTable(
                name: "CourseInstance");

            migrationBuilder.CreateIndex(
                name: "IX_Course_TeacherId",
                table: "Course",
                column: "TeacherId");

            migrationBuilder.AddForeignKey(
                name: "FK_Course_AspNetUsers_TeacherId",
                table: "Course",
                column: "TeacherId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}