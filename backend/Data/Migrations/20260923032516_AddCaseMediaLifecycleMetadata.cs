using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCaseMediaLifecycleMetadata : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "SideBMediaStatus",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AlterColumn<string>(
                name: "SideAMediaStatus",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddColumn<string>(
                name: "SideACaptionStatus",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SideAHeightPixels",
                table: "Cases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SideAMimeType",
                table: "Cases",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SideATranscriptStatus",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SideAWidthPixels",
                table: "Cases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SideBCaptionStatus",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SideBHeightPixels",
                table: "Cases",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SideBMimeType",
                table: "Cases",
                type: "nvarchar(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SideBTranscriptStatus",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "SideBWidthPixels",
                table: "Cases",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SideACaptionStatus",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideAHeightPixels",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideAMimeType",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideATranscriptStatus",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideAWidthPixels",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideBCaptionStatus",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideBHeightPixels",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideBMimeType",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideBTranscriptStatus",
                table: "Cases");

            migrationBuilder.DropColumn(
                name: "SideBWidthPixels",
                table: "Cases");

            migrationBuilder.AlterColumn<int>(
                name: "SideBMediaStatus",
                table: "Cases",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<int>(
                name: "SideAMediaStatus",
                table: "Cases",
                type: "int",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");
        }
    }
}
