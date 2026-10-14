using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddPets : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Pets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Definition = table.Column<int>(type: "INTEGER", nullable: false),
                    Tint = table.Column<int>(type: "INTEGER", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false),
                    Hunger = table.Column<float>(type: "REAL", nullable: false),
                    Hygiene = table.Column<float>(type: "REAL", nullable: false),
                    Play = table.Column<float>(type: "REAL", nullable: false),
                    Mood = table.Column<float>(type: "REAL", nullable: false),
                    Trait = table.Column<int>(type: "INTEGER", nullable: false),
                    TreasureFoundAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    EquippedPetTrick1 = table.Column<int>(type: "INTEGER", nullable: false),
                    EquippedPetTrick2 = table.Column<int>(type: "INTEGER", nullable: false),
                    EquippedPetTrick3 = table.Column<int>(type: "INTEGER", nullable: false),
                    EquippedPetTrick4 = table.Column<int>(type: "INTEGER", nullable: false),
                    ShampooItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    ConditionerItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    ToyItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    BrushItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    FoodBowlItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    FoodItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    VitaminItemId = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pets", x => new { x.Id, x.CharacterId });
                    table.ForeignKey(
                        name: "FK_Pets_Characters_CharacterId",
                        column: x => x.CharacterId,
                        principalTable: "Characters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PetItems",
                columns: table => new
                {
                    ItemsId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemsCharacterId = table.Column<ulong>(type: "INTEGER", nullable: false),
                    PetsId = table.Column<int>(type: "INTEGER", nullable: false),
                    PetsCharacterId = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PetItems", x => new { x.ItemsId, x.ItemsCharacterId, x.PetsId, x.PetsCharacterId });
                    table.ForeignKey(
                        name: "FK_PetItems_Items_ItemsId_ItemsCharacterId",
                        columns: x => new { x.ItemsId, x.ItemsCharacterId },
                        principalTable: "Items",
                        principalColumns: new[] { "Id", "CharacterId" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PetItems_Pets_PetsId_PetsCharacterId",
                        columns: x => new { x.PetsId, x.PetsCharacterId },
                        principalTable: "Pets",
                        principalColumns: new[] { "Id", "CharacterId" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PetItems_PetsId_PetsCharacterId",
                table: "PetItems",
                columns: new[] { "PetsId", "PetsCharacterId" });

            migrationBuilder.CreateIndex(
                name: "IX_Pets_CharacterId",
                table: "Pets",
                column: "CharacterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PetItems");

            migrationBuilder.DropTable(
                name: "Pets");
        }
    }
}
