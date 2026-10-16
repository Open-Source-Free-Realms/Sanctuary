using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AllowItemCopies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_Tint_Definition_CharacterId",
                table: "Items");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Tint_Definition_CharacterId",
                table: "Items",
                columns: new[] { "Tint", "Definition", "CharacterId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Items_Tint_Definition_CharacterId",
                table: "Items");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Tint_Definition_CharacterId",
                table: "Items",
                columns: new[] { "Tint", "Definition", "CharacterId" },
                unique: true);
        }
    }
}
