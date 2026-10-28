using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddHousingGuids : Migration
    {
        private const string HouseColumns = "CharacterId, Created, Description, IsFloraAllowed, IsLocked, IsPublished, Keywords, Name, PetAutospawn, PublishedAt, ZoneId";
        private const string HouseVoteColumns = "VoterId, Category, Created, Rating";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HousingGuids",
                columns: table => new
                {
                    Id = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HousingGuids", x => x.Id);
                });

            migrationBuilder.Sql("""
                CREATE TABLE "ef_temp_Houses" (
                    "Id" INTEGER NOT NULL CONSTRAINT "PK_Houses" PRIMARY KEY,
                    "CharacterId" INTEGER NOT NULL,
                    "Created" TEXT NOT NULL DEFAULT (DATE()),
                    "Description" TEXT NOT NULL,
                    "IsFloraAllowed" INTEGER NOT NULL,
                    "IsLocked" INTEGER NOT NULL,
                    "IsPublished" INTEGER NOT NULL,
                    "Key" INTEGER NOT NULL,
                    "Keywords" TEXT NOT NULL,
                    "Name" TEXT NOT NULL,
                    "PetAutospawn" INTEGER NOT NULL,
                    "PublishedAt" TEXT NULL,
                    "ZoneId" INTEGER NOT NULL,
                    CONSTRAINT "FK_Houses_Characters_CharacterId" FOREIGN KEY ("CharacterId") REFERENCES "Characters" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql($"INSERT INTO ef_temp_Houses (Id, \"Key\", {HouseColumns}) SELECT ROW_NUMBER() OVER (ORDER BY CharacterId, Id), Id, {HouseColumns} FROM Houses;");

            migrationBuilder.Sql("""
                CREATE TABLE "ef_temp_HouseVotes" (
                    "HouseId" INTEGER NOT NULL,
                    "VoterId" INTEGER NOT NULL,
                    "Category" TEXT NOT NULL,
                    "Created" TEXT NOT NULL DEFAULT (DATE()),
                    "Rating" REAL NOT NULL,
                    CONSTRAINT "PK_HouseVotes" PRIMARY KEY ("HouseId", "VoterId"),
                    CONSTRAINT "FK_HouseVotes_Houses_HouseId" FOREIGN KEY ("HouseId") REFERENCES "ef_temp_Houses" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql($"INSERT INTO ef_temp_HouseVotes (HouseId, {HouseVoteColumns}) SELECT ef_temp_Houses.Id, {Qualify("HouseVotes", HouseVoteColumns)} FROM HouseVotes JOIN ef_temp_Houses ON ef_temp_Houses.CharacterId = HouseVotes.CharacterId AND ef_temp_Houses.\"Key\" = HouseVotes.HouseId;");
            migrationBuilder.Sql("INSERT INTO HousingGuids (Id, Created) SELECT Id, Created FROM ef_temp_Houses;");

            migrationBuilder.Sql("DROP TABLE HouseVotes;");
            migrationBuilder.Sql("DROP TABLE Houses;");
            migrationBuilder.Sql("ALTER TABLE ef_temp_Houses RENAME TO Houses;");
            migrationBuilder.Sql("ALTER TABLE ef_temp_HouseVotes RENAME TO HouseVotes;");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_CharacterId_Key",
                table: "Houses",
                columns: new[] { "CharacterId", "Key" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE "ef_temp_Houses" (
                    "Id" INTEGER NOT NULL,
                    "CharacterId" INTEGER NOT NULL,
                    "Created" TEXT NOT NULL DEFAULT (DATE()),
                    "Description" TEXT NOT NULL,
                    "IsFloraAllowed" INTEGER NOT NULL,
                    "IsLocked" INTEGER NOT NULL,
                    "IsPublished" INTEGER NOT NULL,
                    "Keywords" TEXT NOT NULL,
                    "Name" TEXT NOT NULL,
                    "PetAutospawn" INTEGER NOT NULL,
                    "PublishedAt" TEXT NULL,
                    "ZoneId" INTEGER NOT NULL,
                    CONSTRAINT "PK_Houses" PRIMARY KEY ("Id", "CharacterId"),
                    CONSTRAINT "FK_Houses_Characters_CharacterId" FOREIGN KEY ("CharacterId") REFERENCES "Characters" ("Id") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql($"INSERT INTO ef_temp_Houses (Id, {HouseColumns}) SELECT \"Key\", {HouseColumns} FROM Houses;");

            migrationBuilder.Sql("""
                CREATE TABLE "ef_temp_HouseVotes" (
                    "HouseId" INTEGER NOT NULL,
                    "CharacterId" INTEGER NOT NULL,
                    "VoterId" INTEGER NOT NULL,
                    "Category" TEXT NOT NULL,
                    "Created" TEXT NOT NULL DEFAULT (DATE()),
                    "Rating" REAL NOT NULL,
                    CONSTRAINT "PK_HouseVotes" PRIMARY KEY ("HouseId", "CharacterId", "VoterId"),
                    CONSTRAINT "FK_HouseVotes_Houses_HouseId_CharacterId" FOREIGN KEY ("HouseId", "CharacterId") REFERENCES "ef_temp_Houses" ("Id", "CharacterId") ON DELETE CASCADE
                );
                """);

            migrationBuilder.Sql($"INSERT INTO ef_temp_HouseVotes (HouseId, CharacterId, {HouseVoteColumns}) SELECT Houses.\"Key\", Houses.CharacterId, {Qualify("HouseVotes", HouseVoteColumns)} FROM HouseVotes JOIN Houses ON Houses.Id = HouseVotes.HouseId;");

            migrationBuilder.Sql("DROP TABLE HouseVotes;");
            migrationBuilder.Sql("DROP TABLE Houses;");
            migrationBuilder.Sql("ALTER TABLE ef_temp_Houses RENAME TO Houses;");
            migrationBuilder.Sql("ALTER TABLE ef_temp_HouseVotes RENAME TO HouseVotes;");

            migrationBuilder.CreateIndex(
                name: "IX_Houses_CharacterId",
                table: "Houses",
                column: "CharacterId");

            migrationBuilder.DropTable(
                name: "HousingGuids");
        }

        private static string Qualify(string table, string columns) => string.Join(", ", columns.Split(", ").Select(column => $"{table}.{column}"));
    }
}
