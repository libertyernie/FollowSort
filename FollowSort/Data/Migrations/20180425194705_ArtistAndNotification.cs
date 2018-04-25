using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace FollowSort.Data.Migrations
{
    public partial class ArtistAndNotification : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Artists",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    IncludeReposts = table.Column<bool>(nullable: false),
                    IncludeTextPosts = table.Column<bool>(nullable: false),
                    LastChecked = table.Column<DateTimeOffset>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    SourceSite = table.Column<int>(nullable: false),
                    UserId = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Artists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(nullable: false),
                    ArtistName = table.Column<string>(nullable: false),
                    Name = table.Column<string>(nullable: true),
                    PostDate = table.Column<DateTimeOffset>(nullable: false),
                    Repost = table.Column<bool>(nullable: false),
                    SourceSite = table.Column<int>(nullable: false),
                    TextPost = table.Column<bool>(nullable: false),
                    ThumbnailUrl = table.Column<string>(nullable: true),
                    Url = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Artists");

            migrationBuilder.DropTable(
                name: "Notifications");
        }
    }
}
