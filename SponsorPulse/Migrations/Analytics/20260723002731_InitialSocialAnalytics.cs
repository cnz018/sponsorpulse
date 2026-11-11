using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SponsorPulse.Migrations.Analytics
{
    /// <inheritdoc />
    public partial class InitialSocialAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SocialPosts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Platform = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorId = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorName = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorHandle = table.Column<string>(type: "TEXT", nullable: false),
                    AuthorFollowersCount = table.Column<long>(type: "INTEGER", nullable: false),
                    AuthorProfileImageUrl = table.Column<string>(type: "TEXT", nullable: true),
                    ContentText = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: true),
                    LikesCount = table.Column<int>(type: "INTEGER", nullable: false),
                    SharesCount = table.Column<int>(type: "INTEGER", nullable: false),
                    CommentsCount = table.Column<int>(type: "INTEGER", nullable: false),
                    RawJsonPayload = table.Column<string>(type: "TEXT", nullable: false),
                    PlatformSpecificDataJson = table.Column<string>(type: "TEXT", nullable: false),
                    FetchedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SocialPosts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SocialPosts_CreatedAt",
                table: "SocialPosts",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SocialPosts_Platform",
                table: "SocialPosts",
                column: "Platform");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SocialPosts");
        }
    }
}
