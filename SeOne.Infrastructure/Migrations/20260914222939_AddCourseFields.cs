using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SeOne.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCourseFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Duration",
                table: "Course",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "Course",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Level",
                table: "Course",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "Course",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Duration",
                table: "Course");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "Course");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Course");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "Course");
        }
    }
}
