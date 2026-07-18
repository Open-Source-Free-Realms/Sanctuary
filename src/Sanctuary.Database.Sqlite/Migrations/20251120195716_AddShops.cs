using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddShops : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Users_UserGuid",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Friends_Characters_CharacterGuid",
                table: "Friends");

            migrationBuilder.DropForeignKey(
                name: "FK_Friends_Characters_FriendCharacterGuid",
                table: "Friends");

            migrationBuilder.DropForeignKey(
                name: "FK_Ignores_Characters_CharacterGuid",
                table: "Ignores");

            migrationBuilder.DropForeignKey(
                name: "FK_Ignores_Characters_IgnoreCharacterGuid",
                table: "Ignores");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Characters_CharacterGuid",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Mounts_Characters_CharacterGuid",
                table: "Mounts");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileItems_Items_ItemsId_ItemsCharacterGuid",
                table: "ProfileItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileItems_Profiles_ProfilesId_ProfilesCharacterGuid",
                table: "ProfileItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Profiles_Characters_CharacterGuid",
                table: "Profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Titles_Characters_CharacterGuid",
                table: "Titles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Mounts",
                table: "Mounts");

            migrationBuilder.DropIndex(
                name: "IX_Mounts_CharacterGuid",
                table: "Mounts");

            migrationBuilder.DropIndex(
                name: "IX_Items_Definition_CharacterGuid",
                table: "Items");

            migrationBuilder.RenameColumn(
                name: "Guid",
                table: "Users",
                newName: "Id");

            migrationBuilder.RenameColumn(
                name: "CharacterGuid",
                table: "Titles",
                newName: "CharacterId");

            migrationBuilder.RenameIndex(
                name: "IX_Titles_CharacterGuid",
                table: "Titles",
                newName: "IX_Titles_CharacterId");

            migrationBuilder.RenameColumn(
                name: "CharacterGuid",
                table: "Profiles",
                newName: "CharacterId");

            migrationBuilder.RenameIndex(
                name: "IX_Profiles_CharacterGuid",
                table: "Profiles",
                newName: "IX_Profiles_CharacterId");

            migrationBuilder.RenameColumn(
                name: "ProfilesCharacterGuid",
                table: "ProfileItems",
                newName: "ProfilesCharacterId");

            migrationBuilder.RenameColumn(
                name: "ItemsCharacterGuid",
                table: "ProfileItems",
                newName: "ItemsCharacterId");

            migrationBuilder.RenameIndex(
                name: "IX_ProfileItems_ProfilesId_ProfilesCharacterGuid",
                table: "ProfileItems",
                newName: "IX_ProfileItems_ProfilesId_ProfilesCharacterId");

            migrationBuilder.RenameColumn(
                name: "CharacterGuid",
                table: "Mounts",
                newName: "Tint");

            migrationBuilder.RenameColumn(
                name: "CharacterGuid",
                table: "Items",
                newName: "CharacterId");

            migrationBuilder.RenameIndex(
                name: "IX_Items_CharacterGuid",
                table: "Items",
                newName: "IX_Items_CharacterId");

            migrationBuilder.RenameColumn(
                name: "CharacterGuid",
                table: "Ignores",
                newName: "CharacterId");

            migrationBuilder.RenameColumn(
                name: "IgnoreCharacterGuid",
                table: "Ignores",
                newName: "IgnoreCharacterId");

            migrationBuilder.RenameIndex(
                name: "IX_Ignores_CharacterGuid",
                table: "Ignores",
                newName: "IX_Ignores_CharacterId");

            migrationBuilder.RenameColumn(
                name: "CharacterGuid",
                table: "Friends",
                newName: "CharacterId");

            migrationBuilder.RenameColumn(
                name: "FriendCharacterGuid",
                table: "Friends",
                newName: "FriendCharacterId");

            migrationBuilder.RenameIndex(
                name: "IX_Friends_CharacterGuid",
                table: "Friends",
                newName: "IX_Friends_CharacterId");

            migrationBuilder.RenameColumn(
                name: "UserGuid",
                table: "Characters",
                newName: "UserId");

            migrationBuilder.RenameColumn(
                name: "Guid",
                table: "Characters",
                newName: "Id");

            migrationBuilder.RenameIndex(
                name: "IX_Characters_UserGuid",
                table: "Characters",
                newName: "IX_Characters_UserId");

            migrationBuilder.AddColumn<ulong>(
                name: "CharacterId",
                table: "Mounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0ul);

            migrationBuilder.AddColumn<int>(
                name: "Definition",
                table: "Mounts",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<int>(
                name: "Count",
                table: "Items",
                type: "INTEGER",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "INTEGER",
                oldDefaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Coins",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "FacePaintId",
                table: "Characters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HairId",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "HeadId",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ModelCustomizationId",
                table: "Characters",
                type: "INTEGER",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "SkinToneId",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "StationCash",
                table: "Characters",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddPrimaryKey(
                name: "PK_Mounts",
                table: "Mounts",
                columns: new[] { "Id", "CharacterId" });

            migrationBuilder.CreateIndex(
                name: "IX_Mounts_CharacterId",
                table: "Mounts",
                column: "CharacterId");

            migrationBuilder.CreateIndex(
                name: "IX_Mounts_Tint_Definition_CharacterId",
                table: "Mounts",
                columns: new[] { "Tint", "Definition", "CharacterId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Items_Tint_Definition_CharacterId",
                table: "Items",
                columns: new[] { "Tint", "Definition", "CharacterId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Users_UserId",
                table: "Characters",
                column: "UserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Friends_Characters_CharacterId",
                table: "Friends",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Friends_Characters_FriendCharacterId",
                table: "Friends",
                column: "FriendCharacterId",
                principalTable: "Characters",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Ignores_Characters_CharacterId",
                table: "Ignores",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ignores_Characters_IgnoreCharacterId",
                table: "Ignores",
                column: "IgnoreCharacterId",
                principalTable: "Characters",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Characters_CharacterId",
                table: "Items",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Mounts_Characters_CharacterId",
                table: "Mounts",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileItems_Items_ItemsId_ItemsCharacterId",
                table: "ProfileItems",
                columns: new[] { "ItemsId", "ItemsCharacterId" },
                principalTable: "Items",
                principalColumns: new[] { "Id", "CharacterId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileItems_Profiles_ProfilesId_ProfilesCharacterId",
                table: "ProfileItems",
                columns: new[] { "ProfilesId", "ProfilesCharacterId" },
                principalTable: "Profiles",
                principalColumns: new[] { "Id", "CharacterId" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Profiles_Characters_CharacterId",
                table: "Profiles",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Titles_Characters_CharacterId",
                table: "Titles",
                column: "CharacterId",
                principalTable: "Characters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Characters_Users_UserId",
                table: "Characters");

            migrationBuilder.DropForeignKey(
                name: "FK_Friends_Characters_CharacterId",
                table: "Friends");

            migrationBuilder.DropForeignKey(
                name: "FK_Friends_Characters_FriendCharacterId",
                table: "Friends");

            migrationBuilder.DropForeignKey(
                name: "FK_Ignores_Characters_CharacterId",
                table: "Ignores");

            migrationBuilder.DropForeignKey(
                name: "FK_Ignores_Characters_IgnoreCharacterId",
                table: "Ignores");

            migrationBuilder.DropForeignKey(
                name: "FK_Items_Characters_CharacterId",
                table: "Items");

            migrationBuilder.DropForeignKey(
                name: "FK_Mounts_Characters_CharacterId",
                table: "Mounts");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileItems_Items_ItemsId_ItemsCharacterId",
                table: "ProfileItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProfileItems_Profiles_ProfilesId_ProfilesCharacterId",
                table: "ProfileItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Profiles_Characters_CharacterId",
                table: "Profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_Titles_Characters_CharacterId",
                table: "Titles");

            migrationBuilder.DropPrimaryKey(
                name: "PK_Mounts",
                table: "Mounts");

            migrationBuilder.DropIndex(
                name: "IX_Mounts_CharacterId",
                table: "Mounts");

            migrationBuilder.DropIndex(
                name: "IX_Mounts_Tint_Definition_CharacterId",
                table: "Mounts");

            migrationBuilder.DropIndex(
                name: "IX_Items_Tint_Definition_CharacterId",
                table: "Items");

            migrationBuilder.DropColumn(
                name: "CharacterId",
                table: "Mounts");

            migrationBuilder.DropColumn(
                name: "Definition",
                table: "Mounts");

            migrationBuilder.DropColumn(
                name: "Coins",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "FacePaintId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "HairId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "HeadId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "ModelCustomizationId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "SkinToneId",
                table: "Characters");

            migrationBuilder.DropColumn(
                name: "StationCash",
                table: "Characters");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Users",
                newName: "Guid");

            migrationBuilder.RenameColumn(
                name: "CharacterId",
                table: "Titles",
                newName: "CharacterGuid");

            migrationBuilder.RenameIndex(
                name: "IX_Titles_CharacterId",
                table: "Titles",
                newName: "IX_Titles_CharacterGuid");

            migrationBuilder.RenameColumn(
                name: "CharacterId",
                table: "Profiles",
                newName: "CharacterGuid");

            migrationBuilder.RenameIndex(
                name: "IX_Profiles_CharacterId",
                table: "Profiles",
                newName: "IX_Profiles_CharacterGuid");

            migrationBuilder.RenameColumn(
                name: "ProfilesCharacterId",
                table: "ProfileItems",
                newName: "ProfilesCharacterGuid");

            migrationBuilder.RenameColumn(
                name: "ItemsCharacterId",
                table: "ProfileItems",
                newName: "ItemsCharacterGuid");

            migrationBuilder.RenameIndex(
                name: "IX_ProfileItems_ProfilesId_ProfilesCharacterId",
                table: "ProfileItems",
                newName: "IX_ProfileItems_ProfilesId_ProfilesCharacterGuid");

            migrationBuilder.RenameColumn(
                name: "Tint",
                table: "Mounts",
                newName: "CharacterGuid");

            migrationBuilder.RenameColumn(
                name: "CharacterId",
                table: "Items",
                newName: "CharacterGuid");

            migrationBuilder.RenameIndex(
                name: "IX_Items_CharacterId",
                table: "Items",
                newName: "IX_Items_CharacterGuid");

            migrationBuilder.RenameColumn(
                name: "CharacterId",
                table: "Ignores",
                newName: "CharacterGuid");

            migrationBuilder.RenameColumn(
                name: "IgnoreCharacterId",
                table: "Ignores",
                newName: "IgnoreCharacterGuid");

            migrationBuilder.RenameIndex(
                name: "IX_Ignores_CharacterId",
                table: "Ignores",
                newName: "IX_Ignores_CharacterGuid");

            migrationBuilder.RenameColumn(
                name: "CharacterId",
                table: "Friends",
                newName: "CharacterGuid");

            migrationBuilder.RenameColumn(
                name: "FriendCharacterId",
                table: "Friends",
                newName: "FriendCharacterGuid");

            migrationBuilder.RenameIndex(
                name: "IX_Friends_CharacterId",
                table: "Friends",
                newName: "IX_Friends_CharacterGuid");

            migrationBuilder.RenameColumn(
                name: "UserId",
                table: "Characters",
                newName: "UserGuid");

            migrationBuilder.RenameColumn(
                name: "Id",
                table: "Characters",
                newName: "Guid");

            migrationBuilder.RenameIndex(
                name: "IX_Characters_UserId",
                table: "Characters",
                newName: "IX_Characters_UserGuid");

            migrationBuilder.AlterColumn<int>(
                name: "Count",
                table: "Items",
                type: "INTEGER",
                nullable: false,
                defaultValue: 1,
                oldClrType: typeof(int),
                oldType: "INTEGER");

            migrationBuilder.AddPrimaryKey(
                name: "PK_Mounts",
                table: "Mounts",
                columns: new[] { "Id", "CharacterGuid" });

            migrationBuilder.CreateIndex(
                name: "IX_Mounts_CharacterGuid",
                table: "Mounts",
                column: "CharacterGuid");

            migrationBuilder.CreateIndex(
                name: "IX_Items_Definition_CharacterGuid",
                table: "Items",
                columns: new[] { "Definition", "CharacterGuid" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Characters_Users_UserGuid",
                table: "Characters",
                column: "UserGuid",
                principalTable: "Users",
                principalColumn: "Guid",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Friends_Characters_CharacterGuid",
                table: "Friends",
                column: "CharacterGuid",
                principalTable: "Characters",
                principalColumn: "Guid",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Friends_Characters_FriendCharacterGuid",
                table: "Friends",
                column: "FriendCharacterGuid",
                principalTable: "Characters",
                principalColumn: "Guid");

            migrationBuilder.AddForeignKey(
                name: "FK_Ignores_Characters_CharacterGuid",
                table: "Ignores",
                column: "CharacterGuid",
                principalTable: "Characters",
                principalColumn: "Guid",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Ignores_Characters_IgnoreCharacterGuid",
                table: "Ignores",
                column: "IgnoreCharacterGuid",
                principalTable: "Characters",
                principalColumn: "Guid");

            migrationBuilder.AddForeignKey(
                name: "FK_Items_Characters_CharacterGuid",
                table: "Items",
                column: "CharacterGuid",
                principalTable: "Characters",
                principalColumn: "Guid",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Mounts_Characters_CharacterGuid",
                table: "Mounts",
                column: "CharacterGuid",
                principalTable: "Characters",
                principalColumn: "Guid",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileItems_Items_ItemsId_ItemsCharacterGuid",
                table: "ProfileItems",
                columns: new[] { "ItemsId", "ItemsCharacterGuid" },
                principalTable: "Items",
                principalColumns: new[] { "Id", "CharacterGuid" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProfileItems_Profiles_ProfilesId_ProfilesCharacterGuid",
                table: "ProfileItems",
                columns: new[] { "ProfilesId", "ProfilesCharacterGuid" },
                principalTable: "Profiles",
                principalColumns: new[] { "Id", "CharacterGuid" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Profiles_Characters_CharacterGuid",
                table: "Profiles",
                column: "CharacterGuid",
                principalTable: "Characters",
                principalColumn: "Guid",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Titles_Characters_CharacterGuid",
                table: "Titles",
                column: "CharacterGuid",
                principalTable: "Characters",
                principalColumn: "Guid",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
