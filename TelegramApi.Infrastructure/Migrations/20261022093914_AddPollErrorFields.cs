using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPollErrorFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ErrorDate",
                table: "Polls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ErrorText",
                table: "Polls",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ErrorDate",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "ErrorText",
                table: "Polls");
        }
    }
}
