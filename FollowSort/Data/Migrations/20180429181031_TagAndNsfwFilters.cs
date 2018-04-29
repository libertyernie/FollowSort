using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace FollowSort.Data.Migrations
{
    public partial class TagAndNsfwFilters : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "Nsfw",
                table: "Artists",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "TagFilterStr",
                table: "Artists",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Nsfw",
                table: "Artists");

            migrationBuilder.DropColumn(
                name: "TagFilterStr",
                table: "Artists");
        }
    }
}
