using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations;

/// <inheritdoc />
public partial class AdminAccountUserBind : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "UserId",
            table: "admin_accounts",
            type: "int",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_admin_accounts_UserId",
            table: "admin_accounts",
            column: "UserId");

        migrationBuilder.AddForeignKey(
            name: "FK_admin_accounts_AspNetUsers_UserId",
            table: "admin_accounts",
            column: "UserId",
            principalTable: "AspNetUsers",
            principalColumn: "Id");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_admin_accounts_AspNetUsers_UserId",
            table: "admin_accounts");

        migrationBuilder.DropIndex(
            name: "IX_admin_accounts_UserId",
            table: "admin_accounts");

        migrationBuilder.DropColumn(
            name: "UserId",
            table: "admin_accounts");
    }
}
