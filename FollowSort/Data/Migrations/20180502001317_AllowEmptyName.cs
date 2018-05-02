using Microsoft.EntityFrameworkCore.Migrations;
using System;
using System.Collections.Generic;

namespace FollowSort.Data.Migrations
{
    public partial class AllowEmptyName : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ArtistName",
                table: "Notifications",
                nullable: true,
                oldClrType: typeof(string));
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ArtistName",
                table: "Notifications",
                nullable: false,
                oldClrType: typeof(string),
                oldNullable: true);
        }
    }
}
