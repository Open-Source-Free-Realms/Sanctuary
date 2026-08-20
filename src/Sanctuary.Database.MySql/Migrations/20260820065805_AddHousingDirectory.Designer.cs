using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Sanctuary.Database.MySql;

#nullable disable

namespace Sanctuary.Database.MySql.Migrations
{
    [DbContext(typeof(MySqlDatabaseContext))]
    [Migration("20260820065805_AddHousingDirectory")]
    partial class AddHousingDirectory
    {
        protected override void BuildTargetModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "9.0.17")
                .HasAnnotation("Relational:MaxIdentifierLength", 64);

            MySqlModelBuilderExtensions.AutoIncrementColumns(modelBuilder);

            modelBuilder.Entity("DbItemDbProfile", b =>
                {
                    b.Property<int>("ItemsId")
                        .HasColumnType("int");

                    b.Property<ulong>("ItemsCharacterId")
                        .HasColumnType("bigint unsigned");

                    b.Property<int>("ProfilesId")
                        .HasColumnType("int");

                    b.Property<ulong>("ProfilesCharacterId")
                        .HasColumnType("bigint unsigned");

                    b.HasKey("ItemsId", "ItemsCharacterId", "ProfilesId", "ProfilesCharacterId");

                    b.HasIndex("ProfilesId", "ProfilesCharacterId");

                    b.ToTable("ProfileItems", (string)null);
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbCharacter", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint unsigned");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<ulong>("Id"));

                    b.Property<int>("ActiveProfileId")
                        .HasColumnType("int");

                    b.Property<int?>("ActiveTitleId")
                        .HasColumnType("int");

                    b.Property<int>("ChatBubbleBackgroundColor")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(13951728);

                    b.Property<int>("ChatBubbleForegroundColor")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(408679);

                    b.Property<int>("ChatBubbleSize")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(1);

                    b.Property<int>("Coins")
                        .HasColumnType("int");

                    b.Property<DateTimeOffset>("Created")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime(6)")
                        .HasDefaultValueSql("NOW()");

                    b.Property<int>("EyeColor")
                        .HasColumnType("int");

                    b.Property<string>("FacePaint")
                        .HasColumnType("longtext");

                    b.Property<int?>("FacePaintId")
                        .HasColumnType("int");

                    b.Property<string>("FirstName")
                        .IsRequired()
                        .HasMaxLength(16)
                        .HasColumnType("varchar(16)");

                    b.Property<string>("FullName")
                        .ValueGeneratedOnAddOrUpdate()
                        .HasMaxLength(33)
                        .HasColumnType("varchar(33)")
                        .HasComputedColumnSql("CONCAT_WS(' ', `FirstName`, NULLIF(`LastName`, ''))", true);

                    b.Property<int>("Gender")
                        .HasColumnType("int");

                    b.Property<ulong?>("GuildMemberId")
                        .HasColumnType("bigint unsigned");

                    b.Property<string>("Hair")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("HairColor")
                        .HasColumnType("int");

                    b.Property<int>("HairId")
                        .HasColumnType("int");

                    b.Property<string>("Head")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("HeadId")
                        .HasColumnType("int");

                    b.Property<DateTimeOffset?>("LastLogin")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("LastName")
                        .HasMaxLength(16)
                        .HasColumnType("varchar(16)");

                    b.Property<int>("MembershipStatus")
                        .HasColumnType("int");

                    b.Property<int>("Model")
                        .HasColumnType("int");

                    b.Property<string>("ModelCustomization")
                        .HasColumnType("longtext");

                    b.Property<int?>("ModelCustomizationId")
                        .HasColumnType("int");

                    b.Property<int>("PlayTime")
                        .HasColumnType("int");

                    b.Property<float?>("PositionX")
                        .HasColumnType("float");

                    b.Property<float?>("PositionY")
                        .HasColumnType("float");

                    b.Property<float?>("PositionZ")
                        .HasColumnType("float");

                    b.Property<float?>("RotationX")
                        .HasColumnType("float");

                    b.Property<float?>("RotationZ")
                        .HasColumnType("float");

                    b.Property<string>("SkinTone")
                        .IsRequired()
                        .HasColumnType("longtext");

                    b.Property<int>("SkinToneId")
                        .HasColumnType("int");

                    b.Property<int>("StationCash")
                        .HasColumnType("int");

                    b.Property<Guid?>("Ticket")
                        .HasColumnType("char(36)");

                    b.Property<ulong>("UserId")
                        .HasColumnType("bigint unsigned");

                    b.Property<float>("VipRank")
                        .HasColumnType("float");

                    b.HasKey("Id");

                    b.HasIndex("FullName");

                    b.HasIndex("GuildMemberId")
                        .IsUnique();

                    b.HasIndex("UserId");

                    b.ToTable("Characters");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbFriend", b =>
                {
                    b.Property<ulong>("FriendCharacterId")
                        .HasColumnType("bigint unsigned");

                    b.Property<ulong>("CharacterId")
                        .HasColumnType("bigint unsigned");

                    b.Property<DateTimeOffset>("Created")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime(6)")
                        .HasDefaultValueSql("NOW()");

                    b.HasKey("FriendCharacterId", "CharacterId");

                    b.HasIndex("CharacterId");

                    b.ToTable("Friends");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbGuild", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint unsigned");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<ulong>("Id"));

                    b.Property<DateTimeOffset>("Created")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime(6)")
                        .HasDefaultValueSql("NOW()");

                    b.Property<int>("MaxMembers")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(100);

                    b.Property<string>("Name")
                        .IsRequired()
                        .HasMaxLength(32)
                        .HasColumnType("varchar(32)");

                    b.HasKey("Id");

                    b.HasIndex("Name")
                        .IsUnique();

                    b.ToTable("Guilds");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbGuildMember", b =>
                {
                    b.Property<ulong>("Id")
                        .HasColumnType("bigint unsigned");

                    b.Property<ulong>("GuildId")
                        .HasColumnType("bigint unsigned");

                    b.Property<DateTimeOffset>("Joined")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime(6)")
                        .HasDefaultValueSql("NOW()");

                    b.Property<int>("Role")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("GuildId");

                    b.ToTable("GuildMembers");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbHouse", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint unsigned");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<ulong>("Id"));

                    b.Property<ulong>("CharacterId")
                        .HasColumnType("bigint unsigned");

                    b.Property<DateTimeOffset>("Created")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime(6)")
                        .HasDefaultValueSql("NOW()");

                    b.Property<string>("CustomizationData")
                        .HasColumnType("TEXT");

                    b.Property<string>("Description")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("longtext")
                        .HasDefaultValue("");

                    b.Property<int>("FurnitureScore")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<bool>("IsFloraAllowed")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("tinyint(1)")
                        .HasDefaultValue(true);

                    b.Property<bool>("IsLocked")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("IsMembersOnly")
                        .HasColumnType("tinyint(1)");

                    b.Property<bool>("IsPublished")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("tinyint(1)")
                        .HasDefaultValue(false);

                    b.Property<string>("KeywordList")
                        .IsRequired()
                        .ValueGeneratedOnAdd()
                        .HasColumnType("longtext")
                        .HasDefaultValue("");

                    b.Property<DateTimeOffset>("LastVisited")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime(6)")
                        .HasDefaultValueSql("NOW()");

                    b.Property<int>("MaxFixtureCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(2000);

                    b.Property<int>("MaxLandmarkCount")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<string>("Name")
                        .HasMaxLength(64)
                        .HasColumnType("varchar(64)");

                    b.Property<bool>("PetAutospawn")
                        .HasColumnType("tinyint(1)");

                    b.Property<float>("Rating")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("float")
                        .HasDefaultValue(0f);

                    b.Property<int>("Votes")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(0);

                    b.Property<int>("ZoneDefinitionId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("CharacterId", "ZoneDefinitionId")
                        .IsUnique();

                    b.ToTable("Houses");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbHouseFixture", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<int>("Id"));

                    b.Property<DateTimeOffset>("Created")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime(6)")
                        .HasDefaultValueSql("NOW()");

                    b.Property<string>("CustomizationData")
                        .HasColumnType("TEXT");

                    b.Property<ulong>("HouseId")
                        .HasColumnType("bigint unsigned");

                    b.Property<int>("ItemDefinitionId")
                        .HasColumnType("int");

                    b.Property<Guid>("PlacementToken")
                        .HasColumnType("char(36)");

                    b.Property<float>("PositionW")
                        .HasColumnType("float");

                    b.Property<float>("PositionX")
                        .HasColumnType("float");

                    b.Property<float>("PositionY")
                        .HasColumnType("float");

                    b.Property<float>("PositionZ")
                        .HasColumnType("float");

                    b.Property<float>("RotationW")
                        .HasColumnType("float");

                    b.Property<float>("RotationX")
                        .HasColumnType("float");

                    b.Property<float>("RotationY")
                        .HasColumnType("float");

                    b.Property<float>("RotationZ")
                        .HasColumnType("float");

                    b.Property<float>("Scale")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("float")
                        .HasDefaultValue(1f);

                    b.Property<int>("TintId")
                        .HasColumnType("int");

                    b.HasKey("Id");

                    b.HasIndex("HouseId", "PlacementToken")
                        .IsUnique();

                    b.ToTable("HouseFixtures");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbHouseVote", b =>
                {
                    b.Property<ulong>("HouseId")
                        .HasColumnType("bigint unsigned");

                    b.Property<ulong>("CharacterId")
                        .HasColumnType("bigint unsigned");

                    b.Property<DateTimeOffset>("Created")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime(6)")
                        .HasDefaultValueSql("NOW()");

                    b.Property<int>("Value")
                        .HasColumnType("int");

                    b.HasKey("HouseId", "CharacterId");

                    b.ToTable("HouseVotes");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbIgnore", b =>
                {
                    b.Property<ulong>("IgnoreCharacterId")
                        .HasColumnType("bigint unsigned");

                    b.Property<ulong>("CharacterId")
                        .HasColumnType("bigint unsigned");

                    b.Property<DateTimeOffset>("Created")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime(6)")
                        .HasDefaultValueSql("NOW()");

                    b.HasKey("IgnoreCharacterId", "CharacterId");

                    b.HasIndex("CharacterId");

                    b.ToTable("Ignores");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbItem", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<ulong>("CharacterId")
                        .HasColumnType("bigint unsigned");

                    b.Property<int>("Count")
                        .HasColumnType("int");

                    b.Property<DateTimeOffset>("Created")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime(6)")
                        .HasDefaultValueSql("NOW()");

                    b.Property<int>("Definition")
                        .HasColumnType("int");

                    b.Property<int>("Tint")
                        .HasColumnType("int");

                    b.HasKey("Id", "CharacterId");

                    b.HasIndex("CharacterId");

                    b.HasIndex("Tint", "Definition", "CharacterId")
                        .IsUnique();

                    b.ToTable("Items");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbMount", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<ulong>("CharacterId")
                        .HasColumnType("bigint unsigned");

                    b.Property<DateTimeOffset>("Created")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime(6)")
                        .HasDefaultValueSql("NOW()");

                    b.Property<int>("Definition")
                        .HasColumnType("int");

                    b.Property<bool>("IsUpgraded")
                        .HasColumnType("tinyint(1)");

                    b.Property<int>("Tint")
                        .HasColumnType("int");

                    b.HasKey("Id", "CharacterId");

                    b.HasIndex("CharacterId");

                    b.HasIndex("Tint", "Definition", "CharacterId")
                        .IsUnique();

                    b.ToTable("Mounts");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbProfile", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<ulong>("CharacterId")
                        .HasColumnType("bigint unsigned");

                    b.Property<int>("Level")
                        .HasColumnType("int");

                    b.Property<int>("LevelXP")
                        .HasColumnType("int");

                    b.HasKey("Id", "CharacterId");

                    b.HasIndex("CharacterId");

                    b.ToTable("Profiles");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbTitle", b =>
                {
                    b.Property<int>("Id")
                        .HasColumnType("int");

                    b.Property<ulong>("CharacterId")
                        .HasColumnType("bigint unsigned");

                    b.HasKey("Id", "CharacterId");

                    b.HasIndex("CharacterId");

                    b.ToTable("Titles");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbUser", b =>
                {
                    b.Property<ulong>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("bigint unsigned");

                    MySqlPropertyBuilderExtensions.UseMySqlIdentityColumn(b.Property<ulong>("Id"));

                    b.Property<DateTimeOffset>("Created")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("datetime(6)")
                        .HasDefaultValueSql("NOW()");

                    b.Property<bool>("IsAdmin")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("tinyint(1)")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsMember")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("tinyint(1)")
                        .HasDefaultValue(false);

                    b.Property<bool>("IsMod")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("tinyint(1)")
                        .HasDefaultValue(false);

                    b.Property<DateTimeOffset?>("LastLogin")
                        .HasColumnType("datetime(6)");

                    b.Property<DateTimeOffset?>("LockedUntil")
                        .HasColumnType("datetime(6)");

                    b.Property<int>("MaxCharacters")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("int")
                        .HasDefaultValue(10);

                    b.Property<DateTimeOffset?>("MutedUntil")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("Password")
                        .IsRequired()
                        .HasMaxLength(254)
                        .HasColumnType("varchar(254)");

                    b.Property<string>("Session")
                        .HasMaxLength(32)
                        .HasColumnType("varchar(32)");

                    b.Property<DateTimeOffset?>("SessionCreated")
                        .HasColumnType("datetime(6)");

                    b.Property<string>("Username")
                        .IsRequired()
                        .HasMaxLength(254)
                        .HasColumnType("varchar(254)");

                    b.HasKey("Id");

                    b.HasIndex("Username")
                        .IsUnique();

                    b.ToTable("Users");
                });

            modelBuilder.Entity("DbItemDbProfile", b =>
                {
                    b.HasOne("Sanctuary.Database.Entities.DbItem", null)
                        .WithMany()
                        .HasForeignKey("ItemsId", "ItemsCharacterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Sanctuary.Database.Entities.DbProfile", null)
                        .WithMany()
                        .HasForeignKey("ProfilesId", "ProfilesCharacterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbCharacter", b =>
                {
                    b.HasOne("Sanctuary.Database.Entities.DbGuildMember", "GuildMember")
                        .WithOne("Character")
                        .HasForeignKey("Sanctuary.Database.Entities.DbCharacter", "GuildMemberId")
                        .OnDelete(DeleteBehavior.SetNull);

                    b.HasOne("Sanctuary.Database.Entities.DbUser", "User")
                        .WithMany("Characters")
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("GuildMember");

                    b.Navigation("User");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbFriend", b =>
                {
                    b.HasOne("Sanctuary.Database.Entities.DbCharacter", "Character")
                        .WithMany("Friends")
                        .HasForeignKey("CharacterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Sanctuary.Database.Entities.DbCharacter", "FriendCharacter")
                        .WithMany()
                        .HasForeignKey("FriendCharacterId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Character");

                    b.Navigation("FriendCharacter");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbGuildMember", b =>
                {
                    b.HasOne("Sanctuary.Database.Entities.DbGuild", "Guild")
                        .WithMany("Members")
                        .HasForeignKey("GuildId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Guild");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbHouse", b =>
                {
                    b.HasOne("Sanctuary.Database.Entities.DbCharacter", "Character")
                        .WithMany("Houses")
                        .HasForeignKey("CharacterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Character");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbHouseFixture", b =>
                {
                    b.HasOne("Sanctuary.Database.Entities.DbHouse", "House")
                        .WithMany("Fixtures")
                        .HasForeignKey("HouseId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("House");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbHouseVote", b =>
                {
                    b.HasOne("Sanctuary.Database.Entities.DbHouse", "House")
                        .WithMany("VoteRecords")
                        .HasForeignKey("HouseId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("House");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbIgnore", b =>
                {
                    b.HasOne("Sanctuary.Database.Entities.DbCharacter", "Character")
                        .WithMany("Ignores")
                        .HasForeignKey("CharacterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.HasOne("Sanctuary.Database.Entities.DbCharacter", "IgnoreCharacter")
                        .WithMany()
                        .HasForeignKey("IgnoreCharacterId")
                        .OnDelete(DeleteBehavior.NoAction)
                        .IsRequired();

                    b.Navigation("Character");

                    b.Navigation("IgnoreCharacter");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbItem", b =>
                {
                    b.HasOne("Sanctuary.Database.Entities.DbCharacter", "Character")
                        .WithMany("Items")
                        .HasForeignKey("CharacterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Character");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbMount", b =>
                {
                    b.HasOne("Sanctuary.Database.Entities.DbCharacter", "Character")
                        .WithMany("Mounts")
                        .HasForeignKey("CharacterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Character");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbProfile", b =>
                {
                    b.HasOne("Sanctuary.Database.Entities.DbCharacter", "Character")
                        .WithMany("Profiles")
                        .HasForeignKey("CharacterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Character");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbTitle", b =>
                {
                    b.HasOne("Sanctuary.Database.Entities.DbCharacter", "Character")
                        .WithMany("Titles")
                        .HasForeignKey("CharacterId")
                        .OnDelete(DeleteBehavior.Cascade)
                        .IsRequired();

                    b.Navigation("Character");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbCharacter", b =>
                {
                    b.Navigation("Friends");

                    b.Navigation("Houses");

                    b.Navigation("Ignores");

                    b.Navigation("Items");

                    b.Navigation("Mounts");

                    b.Navigation("Profiles");

                    b.Navigation("Titles");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbGuild", b =>
                {
                    b.Navigation("Members");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbGuildMember", b =>
                {
                    b.Navigation("Character")
                        .IsRequired();
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbHouse", b =>
                {
                    b.Navigation("Fixtures");

                    b.Navigation("VoteRecords");
                });

            modelBuilder.Entity("Sanctuary.Database.Entities.DbUser", b =>
                {
                    b.Navigation("Characters");
                });
#pragma warning restore 612, 618
        }
    }
}
