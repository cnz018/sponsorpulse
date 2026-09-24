using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SponsorPulse.Migrations
{
    /// <inheritdoc />
    public partial class RenameChannelIdToBroadcasterId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ChannelId",
                table: "Events",
                newName: "BroadcasterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "BroadcasterId",
                table: "Events",
                newName: "ChannelId");
        }
    }
}
