using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPollHistoryFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "CreatedAt",
                table: "Polls",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "DeletedAt",
                table: "Polls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "Polls",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "TgAlias",
                table: "Polls",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CreatedAt",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "TgAlias",
                table: "Polls");
        }
    }
}
