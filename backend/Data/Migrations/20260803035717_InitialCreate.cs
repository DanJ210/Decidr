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
                name: "RewardBadges",
                columns: table => new
                {
                    Code = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IconKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Tier = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RewardBadges", x => x.Code);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Cases",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    SideAUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SideAUserName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SideAClaim = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    SideAPostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SideBUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SideBUserName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SideBClaim = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    SideBPostedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    InvitedUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    WinnerSide = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Cases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Cases_Users_InvitedUserId",
                        column: x => x.InvitedUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cases_Users_SideAUserId",
                        column: x => x.SideAUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Cases_Users_SideBUserId",
                        column: x => x.SideBUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FriendRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FriendRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FriendRequests_Users_FromUserId",
                        column: x => x.FromUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_FriendRequests_Users_ToUserId",
                        column: x => x.ToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "UserRewards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BadgeCode = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SourceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    AwardedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UserRewards_RewardBadges_BadgeCode",
                        column: x => x.BadgeCode,
                        principalTable: "RewardBadges",
                        principalColumn: "Code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_UserRewards_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseComments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1024)", maxLength: 1024, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseComments_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaseComments_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseEvidence",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Side = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedByUserName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    ResourceUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    MimeType = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseEvidence", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CaseEvidence_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaseEvidence_Users_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CaseVotes",
                columns: table => new
                {
                    CaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Side = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ChangeCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CaseVotes", x => new { x.CaseId, x.UserId });
                    table.ForeignKey(
                        name: "FK_CaseVotes_Cases_CaseId",
                        column: x => x.CaseId,
                        principalTable: "Cases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CaseVotes_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
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
                name: "IX_CaseComments_CaseId_CreatedAtUtc",
                table: "CaseComments",
                columns: new[] { "CaseId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseComments_UserId",
                table: "CaseComments",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseEvidence_AddedByUserId",
                table: "CaseEvidence",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CaseEvidence_CaseId_Side_CreatedAtUtc",
                table: "CaseEvidence",
                columns: new[] { "CaseId", "Side", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_InvitedUserId_Status",
                table: "Cases",
                columns: new[] { "InvitedUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Cases_SideAUserId",
                table: "Cases",
                column: "SideAUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_SideBUserId",
                table: "Cases",
                column: "SideBUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Cases_Status_CreatedAtUtc",
                table: "Cases",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_CaseVotes_UserId",
                table: "CaseVotes",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_FromUserId_Status",
                table: "FriendRequests",
                columns: new[] { "FromUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FriendRequests_ToUserId_Status",
                table: "FriendRequests",
                columns: new[] { "ToUserId", "Status" });

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CaseComments");

            migrationBuilder.DropTable(
                name: "CaseEvidence");

            migrationBuilder.DropTable(
                name: "CaseVotes");

            migrationBuilder.DropTable(
                name: "FriendRequests");

            migrationBuilder.DropTable(
                name: "UserRewards");

            migrationBuilder.DropTable(
                name: "Cases");

            migrationBuilder.DropTable(
                name: "RewardBadges");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
