using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayTimeColumn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PlayTime",
                table: "Characters",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlayTime",
                table: "Characters");
        }
    }
}
