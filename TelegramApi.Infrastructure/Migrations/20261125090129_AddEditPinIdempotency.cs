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
                name: "IdempotencyHash",
                table: "Publications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Publications",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Publications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedAt",
                table: "Publications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyHash",
                table: "Polls",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdempotencyKey",
                table: "Polls",
                type: "character varying(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsPinned",
                table: "Polls",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PinnedAt",
                table: "Polls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Publications_IdempotencyKey",
                table: "Publications",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_IdempotencyKey",
                table: "Polls",
                column: "IdempotencyKey",
                unique: true,
                filter: "\"IdempotencyKey\" IS NOT NULL");
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
                name: "IdempotencyHash",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "Publications");

            migrationBuilder.DropColumn(
                name: "IdempotencyHash",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "IdempotencyKey",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "IsPinned",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "PinnedAt",
                table: "Polls");
        }
    }
}
