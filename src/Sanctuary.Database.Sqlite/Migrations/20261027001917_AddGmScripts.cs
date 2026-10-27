using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddGmScripts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Conversation",
                table: "WorldNpcs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Script",
                table: "WorldNpcs",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Script",
                table: "WorldFixtures",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Conversation",
                table: "WorldNpcs");

            migrationBuilder.DropColumn(
                name: "Script",
                table: "WorldNpcs");

            migrationBuilder.DropColumn(
                name: "Script",
                table: "WorldFixtures");
        }
    }
}
