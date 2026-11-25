using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TelegramApi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReviewFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "PublicationType",
                table: "Publications",
                newName: "Format");

            migrationBuilder.AlterColumn<string>(
                name: "TgAlias",
                table: "Publications",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "Publications",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorText",
                table: "Publications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TgAlias",
                table: "Polls",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Question",
                table: "Polls",
                type: "character varying(300)",
                maxLength: 300,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ErrorText",
                table: "Polls",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "Polls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ErrorCode",
                table: "Polls",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SentAt",
                table: "Polls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Polls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "PollOption",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "MediaFile",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "MediaFile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TgMessageId",
                table: "MediaFile",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "Button",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "Button",
                type: "character varying(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AddColumn<int>(
                name: "Order",
                table: "Button",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Publications_CreatedAt",
                table: "Publications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_Status",
                table: "Publications",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Publications_TgAlias",
                table: "Publications",
                column: "TgAlias");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_CreatedAt",
                table: "Polls",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_Status",
                table: "Polls",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Polls_TgAlias",
                table: "Polls",
                column: "TgAlias");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Publications_CreatedAt",
                table: "Publications");

            migrationBuilder.DropIndex(
                name: "IX_Publications_Status",
                table: "Publications");

            migrationBuilder.DropIndex(
                name: "IX_Publications_TgAlias",
                table: "Publications");

            migrationBuilder.DropIndex(
                name: "IX_Polls_CreatedAt",
                table: "Polls");

            migrationBuilder.DropIndex(
                name: "IX_Polls_Status",
                table: "Polls");

            migrationBuilder.DropIndex(
                name: "IX_Polls_TgAlias",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "ErrorCode",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "SentAt",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Polls");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "MediaFile");

            migrationBuilder.DropColumn(
                name: "TgMessageId",
                table: "MediaFile");

            migrationBuilder.DropColumn(
                name: "Order",
                table: "Button");

            migrationBuilder.RenameColumn(
                name: "Format",
                table: "Publications",
                newName: "PublicationType");

            migrationBuilder.AlterColumn<string>(
                name: "TgAlias",
                table: "Publications",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "Publications",
                type: "text",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "character varying(4096)",
                oldMaxLength: 4096,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorText",
                table: "Publications",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "TgAlias",
                table: "Polls",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);

            migrationBuilder.AlterColumn<string>(
                name: "Question",
                table: "Polls",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(300)",
                oldMaxLength: 300);

            migrationBuilder.AlterColumn<string>(
                name: "ErrorText",
                table: "Polls",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "PollOption",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "FileName",
                table: "MediaFile",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Url",
                table: "Button",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2048)",
                oldMaxLength: 2048);

            migrationBuilder.AlterColumn<string>(
                name: "Text",
                table: "Button",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(64)",
                oldMaxLength: 64);
        }
    }
}
