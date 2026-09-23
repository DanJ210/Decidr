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

            migrationBuilder.Sql("""
                UPDATE [Cases]
                SET [SideAMediaStatus] = CASE [SideAMediaStatus]
                    WHEN '0' THEN 'None'
                    WHEN '1' THEN 'Pending'
                    WHEN '2' THEN 'Ready'
                    WHEN '3' THEN 'Failed'
                    WHEN '4' THEN 'Uploading'
                    WHEN '5' THEN 'Processing'
                    WHEN '6' THEN 'Rejected'
                    ELSE [SideAMediaStatus]
                END;

                UPDATE [Cases]
                SET [SideBMediaStatus] = CASE [SideBMediaStatus]
                    WHEN '0' THEN 'None'
                    WHEN '1' THEN 'Pending'
                    WHEN '2' THEN 'Ready'
                    WHEN '3' THEN 'Failed'
                    WHEN '4' THEN 'Uploading'
                    WHEN '5' THEN 'Processing'
                    WHEN '6' THEN 'Rejected'
                    ELSE [SideBMediaStatus]
                END;
                """);

            migrationBuilder.AddColumn<string>(
                name: "SideACaptionStatus",
                table: "Cases",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "None");

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
                defaultValue: "None");

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
                defaultValue: "None");

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
                defaultValue: "None");

            migrationBuilder.AddColumn<int>(
                name: "SideBWidthPixels",
                table: "Cases",
                type: "int",
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE [Cases] SET [SideACaptionStatus] = 'None' WHERE [SideACaptionStatus] = '';
                UPDATE [Cases] SET [SideATranscriptStatus] = 'None' WHERE [SideATranscriptStatus] = '';
                UPDATE [Cases] SET [SideBCaptionStatus] = 'None' WHERE [SideBCaptionStatus] = '';
                UPDATE [Cases] SET [SideBTranscriptStatus] = 'None' WHERE [SideBTranscriptStatus] = '';
                """);
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

            migrationBuilder.Sql("""
                UPDATE [Cases]
                SET [SideBMediaStatus] = CASE [SideBMediaStatus]
                    WHEN 'None' THEN '0'
                    WHEN 'Pending' THEN '1'
                    WHEN 'Ready' THEN '2'
                    WHEN 'Failed' THEN '3'
                    WHEN 'Uploading' THEN '4'
                    WHEN 'Processing' THEN '5'
                    WHEN 'Rejected' THEN '6'
                    ELSE [SideBMediaStatus]
                END;

                UPDATE [Cases]
                SET [SideAMediaStatus] = CASE [SideAMediaStatus]
                    WHEN 'None' THEN '0'
                    WHEN 'Pending' THEN '1'
                    WHEN 'Ready' THEN '2'
                    WHEN 'Failed' THEN '3'
                    WHEN 'Uploading' THEN '4'
                    WHEN 'Processing' THEN '5'
                    WHEN 'Rejected' THEN '6'
                    ELSE [SideAMediaStatus]
                END;
                """);

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
