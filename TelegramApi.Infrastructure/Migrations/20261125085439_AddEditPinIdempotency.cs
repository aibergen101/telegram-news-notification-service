using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEditPinIdempotency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EditedAt",
                table: "Publications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Publications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Publications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "Publications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Polls",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Polls",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "RequestHash",
                table: "Polls",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Publications_IdempotencyKey",
                table: "Publications",
                column: "IdempotencyKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Polls_IdempotencyKey",
                table: "Polls",
                column: "IdempotencyKey",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Publications_IdempotencyKey",
                table: "Publications");

            migrationBuilder.DropIndex(
                name: "IX_Polls_IdempotencyKey",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "EditedAt",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "RequestHash",
                table: "Polls");
        }
    }
}
