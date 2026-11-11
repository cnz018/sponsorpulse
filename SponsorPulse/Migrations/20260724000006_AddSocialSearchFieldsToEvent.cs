using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SponsorPulse.Migrations
{
    /// <inheritdoc />
    public partial class AddSocialSearchFieldsToEvent : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "SocialEndDate",
                table: "Events",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "SocialStartDate",
                table: "Events",
                type: "TEXT",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "SocialEndDate", table: "Events");

            migrationBuilder.DropColumn(name: "SocialStartDate", table: "Events");
        }
    }
}
