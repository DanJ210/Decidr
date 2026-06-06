using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace backend.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    Category = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: false),
                    SideAUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    SideAUserName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SideAClaim = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    SideAPostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SideBUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    SideBUserName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SideBClaim = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    SideBPostedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    InvitedUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    WinnerSide = table.Column<string>(type: "text", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CaseVotes",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Side = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChangeCount = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseVotes", x => new { x.CaseId, x.UserId });
                });

            migrationBuilder.CreateTable(
                name: "FriendRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RewardBadges",
                columns: table => new
                {
                    Code = table.Column<string>(type: "text", nullable: false),
                    Label = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    IconKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Tier = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardBadges", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "UserRewards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BadgeCode = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceType = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    AwardedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRewards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserName = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Role = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "RewardBadges",
                columns: new[] { "Code", "Description", "IconKey", "Label", "Tier" },
                values: new object[,]
                {
                    { "CASE_VICTOR", "Awarded to the winning side poster when a case is closed.", "crown", "Court Victor", "Gold" },
                    { "POST_PARTICIPATION", "Awarded for posting a side in a case.", "quill", "Case Contributor", "Bronze" },
                    { "VOTE_PARTICIPATION", "Awarded for participating in community voting.", "jury", "Community Juror", "Bronze" },
                    { "VOTE_WINNER_MATCH", "Awarded when your vote matches the winning side.", "target", "Sharp Eye", "Silver" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_InvitedUserId",
                table: "Cases",
                column: "InvitedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_SideAUserId",
                table: "Cases",
                column: "SideAUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_SideBUserId",
                table: "Cases",
                column: "SideBUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseVotes_UserId",
                table: "CaseVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_FromUserId",
                table: "FriendRequests",
                column: "FromUserId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_ToUserId",
                table: "FriendRequests",
                column: "ToUserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRewards_BadgeCode",
                table: "UserRewards",
                column: "BadgeCode");

            migrationBuilder.CreateIndex(
                name: "IX_UserRewards_UserId_BadgeCode_SourceType_SourceId",
                table: "UserRewards",
                columns: new[] { "UserId", "BadgeCode", "SourceType", "SourceId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_UserName",
                table: "Users",
                column: "UserName",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Cases_Users_InvitedUserId",
                table: "Cases",
                column: "InvitedUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cases_Users_SideAUserId",
                table: "Cases",
                column: "SideAUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Cases_Users_SideBUserId",
                table: "Cases",
                column: "SideBUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CaseVotes_Cases_CaseId",
                table: "CaseVotes",
                column: "CaseId",
                principalTable: "Cases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CaseVotes_Users_UserId",
                table: "CaseVotes",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_Users_FromUserId",
                table: "FriendRequests",
                column: "FromUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FriendRequests_Users_ToUserId",
                table: "FriendRequests",
                column: "ToUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRewards_RewardBadges_BadgeCode",
                table: "UserRewards",
                column: "BadgeCode",
                principalTable: "RewardBadges",
                principalColumn: "Code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserRewards_Users_UserId",
                table: "UserRewards",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseVotes");

            migrationBuilder.DropTable(
                name: "FriendRequests");

            migrationBuilder.DropTable(
                name: "Cases");

            migrationBuilder.DropTable(
                name: "UserRewards");

            migrationBuilder.DropTable(
                name: "RewardBadges");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
