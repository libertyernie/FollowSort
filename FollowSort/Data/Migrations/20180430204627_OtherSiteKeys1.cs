using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace FollowSort.Data.Migrations
{
    public partial class OtherSiteKeys1 : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FurryNetworkBearerToken",
                table: "AspNetUsers",
                type: "CHAR(40)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WeasylApiKey",
                table: "AspNetUsers",
                type: "CHAR(48)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FurryNetworkBearerToken",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "WeasylApiKey",
                table: "AspNetUsers");
        }
    }
}
