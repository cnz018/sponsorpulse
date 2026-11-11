using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SponsorPulse.Migrations.Analytics
{
    /// <inheritdoc />
    public partial class AddImpressionsToSocialPostEntity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ImpressionsCount",
                table: "SocialPosts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImpressionsCount",
                table: "SocialPosts");
        }
    }
}
