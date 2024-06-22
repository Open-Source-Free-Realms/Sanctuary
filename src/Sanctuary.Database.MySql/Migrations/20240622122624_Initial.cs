using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    Guid = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Username = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Password = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Session = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    MaxCharacters = table.Column<int>(type: "int", nullable: false, defaultValue: 10),
                    IsLocked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsMember = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsAdmin = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURDATE()"),
                    LastLogin = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Guid);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Characters",
                columns: table => new
                {
                    Guid = table.Column<ulong>(type: "bigint unsigned", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    Ticket = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    FirstName = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    LastName = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Model = table.Column<int>(type: "int", nullable: false),
                    Head = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Hair = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    ModelCustomization = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    FacePaint = table.Column<string>(type: "longtext", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    SkinTone = table.Column<string>(type: "longtext", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    EyeColor = table.Column<int>(type: "int", nullable: false),
                    HairColor = table.Column<int>(type: "int", nullable: false),
                    ActiveProfileId = table.Column<int>(type: "int", nullable: false),
                    Gender = table.Column<int>(type: "int", nullable: false),
                    ActiveTitleId = table.Column<int>(type: "int", nullable: true),
                    VipRank = table.Column<float>(type: "float", nullable: false),
                    MembershipStatus = table.Column<int>(type: "int", nullable: false),
                    ChatBubbleForegroundColor = table.Column<int>(type: "int", nullable: false, defaultValue: 408679),
                    ChatBubbleBackgroundColor = table.Column<int>(type: "int", nullable: false, defaultValue: 13951728),
                    ChatBubbleSize = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Created = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURDATE()"),
                    LastLogin = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    UserGuid = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Position_W = table.Column<float>(type: "float", nullable: false),
                    Position_X = table.Column<float>(type: "float", nullable: false),
                    Position_Y = table.Column<float>(type: "float", nullable: false),
                    Position_Z = table.Column<float>(type: "float", nullable: false),
                    Rotation_W = table.Column<float>(type: "float", nullable: false),
                    Rotation_X = table.Column<float>(type: "float", nullable: false),
                    Rotation_Y = table.Column<float>(type: "float", nullable: false),
                    Rotation_Z = table.Column<float>(type: "float", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Items",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CharacterGuid = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Tint = table.Column<int>(type: "int", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    Definition = table.Column<int>(type: "int", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURDATE()")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Mounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CharacterGuid = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    IsUpgraded = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Created = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false, defaultValueSql: "CURDATE()")
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Profiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CharacterGuid = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    LevelXP = table.Column<int>(type: "int", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Titles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    CharacterGuid = table.Column<ulong>(type: "bigint unsigned", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ProfileItems",
                columns: table => new
                {
                    ItemsId = table.Column<int>(type: "int", nullable: false),
                    ItemsCharacterGuid = table.Column<ulong>(type: "bigint unsigned", nullable: false),
                    ProfilesId = table.Column<int>(type: "int", nullable: false),
                    ProfilesCharacterGuid = table.Column<ulong>(type: "bigint unsigned", nullable: false)
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
                })
                .Annotation("MySql:CharSet", "utf8mb4");

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
