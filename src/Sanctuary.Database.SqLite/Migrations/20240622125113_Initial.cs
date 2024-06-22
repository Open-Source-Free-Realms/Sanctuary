using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Guid = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Username = table.Column<string>(type: "TEXT", nullable: false),
                    Password = table.Column<string>(type: "TEXT", nullable: false),
                    Session = table.Column<string>(type: "TEXT", nullable: true),
                    MaxCharacters = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 10),
                    IsLocked = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsMember = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsAdmin = table.Column<bool>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()"),
                    LastLogin = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Guid);
                });

            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    Guid = table.Column<ulong>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Ticket = table.Column<Guid>(type: "TEXT", nullable: true),
                    FirstName = table.Column<string>(type: "TEXT", nullable: false),
                    LastName = table.Column<string>(type: "TEXT", nullable: true),
                    Model = table.Column<int>(type: "INTEGER", nullable: false),
                    Head = table.Column<string>(type: "TEXT", nullable: false),
                    Hair = table.Column<string>(type: "TEXT", nullable: false),
                    ModelCustomization = table.Column<string>(type: "TEXT", nullable: true),
                    FacePaint = table.Column<string>(type: "TEXT", nullable: true),
                    SkinTone = table.Column<string>(type: "TEXT", nullable: false),
                    EyeColor = table.Column<int>(type: "INTEGER", nullable: false),
                    HairColor = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveProfileId = table.Column<int>(type: "INTEGER", nullable: false),
                    Gender = table.Column<int>(type: "INTEGER", nullable: false),
                    ActiveTitleId = table.Column<int>(type: "INTEGER", nullable: true),
                    VipRank = table.Column<float>(type: "REAL", nullable: false),
                    MembershipStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    ChatBubbleForegroundColor = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 408679),
                    ChatBubbleBackgroundColor = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 13951728),
                    ChatBubbleSize = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()"),
                    LastLogin = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    UserGuid = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Position_W = table.Column<float>(type: "REAL", nullable: false),
                    Position_X = table.Column<float>(type: "REAL", nullable: false),
                    Position_Y = table.Column<float>(type: "REAL", nullable: false),
                    Position_Z = table.Column<float>(type: "REAL", nullable: false),
                    Rotation_W = table.Column<float>(type: "REAL", nullable: false),
                    Rotation_X = table.Column<float>(type: "REAL", nullable: false),
                    Rotation_Y = table.Column<float>(type: "REAL", nullable: false),
                    Rotation_Z = table.Column<float>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Characters", x => x.Guid);
                    table.ForeignKey(
                        name: "FK_Characters_Users_UserGuid",
                        column: x => x.UserGuid,
                        principalTable: "Users",
                        principalColumn: "Guid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterGuid = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Tint = table.Column<int>(type: "INTEGER", nullable: false),
                    Count = table.Column<int>(type: "INTEGER", nullable: false, defaultValue: 1),
                    Definition = table.Column<int>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Items", x => new { x.Id, x.CharacterGuid });
                    table.ForeignKey(
                        name: "FK_Items_Characters_CharacterGuid",
                        column: x => x.CharacterGuid,
                        principalTable: "Characters",
                        principalColumn: "Guid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Mounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterGuid = table.Column<ulong>(type: "INTEGER", nullable: false),
                    IsUpgraded = table.Column<bool>(type: "INTEGER", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "TEXT", nullable: false, defaultValueSql: "DATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Mounts", x => new { x.Id, x.CharacterGuid });
                    table.ForeignKey(
                        name: "FK_Mounts_Characters_CharacterGuid",
                        column: x => x.CharacterGuid,
                        principalTable: "Characters",
                        principalColumn: "Guid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterGuid = table.Column<ulong>(type: "INTEGER", nullable: false),
                    Level = table.Column<int>(type: "INTEGER", nullable: false),
                    LevelXP = table.Column<int>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Profiles", x => new { x.Id, x.CharacterGuid });
                    table.ForeignKey(
                        name: "FK_Profiles_Characters_CharacterGuid",
                        column: x => x.CharacterGuid,
                        principalTable: "Characters",
                        principalColumn: "Guid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Titles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false),
                    CharacterGuid = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Titles", x => new { x.Id, x.CharacterGuid });
                    table.ForeignKey(
                        name: "FK_Titles_Characters_CharacterGuid",
                        column: x => x.CharacterGuid,
                        principalTable: "Characters",
                        principalColumn: "Guid",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProfileItems",
                columns: table => new
                {
                    ItemsId = table.Column<int>(type: "INTEGER", nullable: false),
                    ItemsCharacterGuid = table.Column<ulong>(type: "INTEGER", nullable: false),
                    ProfilesId = table.Column<int>(type: "INTEGER", nullable: false),
                    ProfilesCharacterGuid = table.Column<ulong>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProfileItems", x => new { x.ItemsId, x.ItemsCharacterGuid, x.ProfilesId, x.ProfilesCharacterGuid });
                    table.ForeignKey(
                        name: "FK_ProfileItems_Items_ItemsId_ItemsCharacterGuid",
                        columns: x => new { x.ItemsId, x.ItemsCharacterGuid },
                        principalTable: "Items",
                        principalColumns: new[] { "Id", "CharacterGuid" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProfileItems_Profiles_ProfilesId_ProfilesCharacterGuid",
                        columns: x => new { x.ProfilesId, x.ProfilesCharacterGuid },
                        principalTable: "Profiles",
                        principalColumns: new[] { "Id", "CharacterGuid" },
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "Users",
                columns: new[] { "Guid", "Created", "IsAdmin", "IsLocked", "IsMember", "LastLogin", "MaxCharacters", "Password", "Session", "Username" },
                values: new object[] { 1ul, new DateTimeOffset(new DateTime(2024, 6, 22, 13, 51, 13, 273, DateTimeKind.Unspecified).AddTicks(6902), new TimeSpan(0, 1, 0, 0, 0)), true, false, true, null, 10, "admin", "EXmdPd5dbAcs58vZ0iCcPRtJkGdMePL2", "admin" });

            migrationBuilder.CreateIndex(
                name: "IX_Characters_UserGuid",
                table: "Characters",
                column: "UserGuid");

            migrationBuilder.CreateIndex(
                name: "IX_Items_CharacterGuid",
                table: "Items",
                column: "CharacterGuid");

            migrationBuilder.CreateIndex(
                name: "IX_Mounts_CharacterGuid",
                table: "Mounts",
                column: "CharacterGuid");

            migrationBuilder.CreateIndex(
                name: "IX_ProfileItems_ProfilesId_ProfilesCharacterGuid",
                table: "ProfileItems",
                columns: new[] { "ProfilesId", "ProfilesCharacterGuid" });

            migrationBuilder.CreateIndex(
                name: "IX_Profiles_CharacterGuid",
                table: "Profiles",
                column: "CharacterGuid");

            migrationBuilder.CreateIndex(
                name: "IX_Titles_CharacterGuid",
                table: "Titles",
                column: "CharacterGuid");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Mounts");

            migrationBuilder.DropTable(
                name: "ProfileItems");

            migrationBuilder.DropTable(
                name: "Titles");

            migrationBuilder.DropTable(
                name: "Items");

            migrationBuilder.DropTable(
                name: "Profiles");

            migrationBuilder.DropTable(
                name: "Characters");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
