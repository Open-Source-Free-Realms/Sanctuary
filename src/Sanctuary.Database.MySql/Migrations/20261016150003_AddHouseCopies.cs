using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class AddHouseCopies : Migration
    {
        private const int ReservedHouseKeyFlag = 1 << 30;

        private static readonly (int ZoneId, int HouseDefinitionId)[] ReservedHouses =
        [
            (24, 24),
            (25, 25),
            (106, 7),
            (122, 23)
        ];

        private const string HouseColumns = "CharacterId, Name, IsLocked, IsFloraAllowed, PetAutospawn, Created, Description, IsPublished, Keywords, PublishedAt";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ZoneId",
                table: "Houses",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("UPDATE Houses SET ZoneId = Id;");

            var reservedZoneIds = string.Join(", ", ReservedHouses.Select(house => house.ZoneId));

            migrationBuilder.Sql($"DELETE FROM HouseVotes WHERE HouseId NOT IN ({reservedZoneIds});");
            migrationBuilder.Sql($"DELETE FROM Houses WHERE Id NOT IN ({reservedZoneIds});");

            foreach (var (zoneId, houseDefinitionId) in ReservedHouses)
                MoveHouse(migrationBuilder, zoneId, ReservedHouseKeyFlag | houseDefinitionId);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            var reservedHouseKeys = string.Join(", ", ReservedHouses.Select(house => ReservedHouseKeyFlag | house.HouseDefinitionId));

            migrationBuilder.Sql($"DELETE FROM HouseVotes WHERE HouseId NOT IN ({reservedHouseKeys});");
            migrationBuilder.Sql($"DELETE FROM Houses WHERE Id NOT IN ({reservedHouseKeys});");

            foreach (var (zoneId, houseDefinitionId) in ReservedHouses)
                MoveHouse(migrationBuilder, ReservedHouseKeyFlag | houseDefinitionId, zoneId);

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "Houses");
        }

        private static void MoveHouse(MigrationBuilder migrationBuilder, int fromId, int toId)
        {
            migrationBuilder.Sql($"INSERT INTO Houses (Id, ZoneId, {HouseColumns}) SELECT {toId}, ZoneId, {HouseColumns} FROM Houses WHERE Id = {fromId};");
            migrationBuilder.Sql($"UPDATE HouseVotes SET HouseId = {toId} WHERE HouseId = {fromId};");
            migrationBuilder.Sql($"DELETE FROM Houses WHERE Id = {fromId};");
        }
    }
}
