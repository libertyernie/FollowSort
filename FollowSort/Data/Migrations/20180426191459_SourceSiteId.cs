using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace FollowSort.Data.Migrations
{
    public partial class SourceSiteId : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SourceSiteId",
                table: "Notifications",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastCheckedSourceSiteId",
                table: "Artists",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SourceSiteId",
                table: "Notifications");

            migrationBuilder.DropColumn(
                name: "LastCheckedSourceSiteId",
                table: "Artists");
        }
    }
}
