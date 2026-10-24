using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations;

/// <inheritdoc />
public partial class AddAdminAccounts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "AdminAccountId",
            table: "admin_audit_logs",
            type: "int",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AdminAccountName",
            table: "admin_audit_logs",
            type: "varchar(128)",
            maxLength: 128,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateTable(
            name: "admin_accounts",
            columns: table => new
            {
                Id = table.Column<int>(type: "int", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                Provider = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Subject = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                UserName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                IsEnabled = table.Column<bool>(type: "tinyint(1)", nullable: false),
                IsBootstrap = table.Column<bool>(type: "tinyint(1)", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                LastLoginAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_admin_accounts", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_admin_accounts_Provider_Subject",
            table: "admin_accounts",
            columns: new[] { "Provider", "Subject" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "admin_accounts");

        migrationBuilder.DropColumn(
            name: "AdminAccountId",
            table: "admin_audit_logs");

        migrationBuilder.DropColumn(
            name: "AdminAccountName",
            table: "admin_audit_logs");
    }
}
