using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseMedia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SideADurationSeconds",
                table: "Cases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SideAMediaStatus",
                table: "Cases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SideAMediaUrl",
                table: "Cases",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SideAThumbnailUrl",
                table: "Cases",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SideBDurationSeconds",
                table: "Cases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SideBMediaStatus",
                table: "Cases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SideBMediaUrl",
                table: "Cases",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SideBThumbnailUrl",
                table: "Cases",
                type: "nvarchar(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SideADurationSeconds",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideAMediaStatus",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideAMediaUrl",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideAThumbnailUrl",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideBDurationSeconds",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideBMediaStatus",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideBMediaUrl",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideBThumbnailUrl",
                table: "Cases");
        }
    }
}
