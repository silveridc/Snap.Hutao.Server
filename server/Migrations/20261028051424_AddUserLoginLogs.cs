using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations;

/// <inheritdoc />
public partial class AddUserLoginLogs : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "user_login_logs",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                UserName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                UserId = table.Column<int>(type: "int", nullable: true),
                Method = table.Column<string>(type: "varchar(16)", maxLength: 16, nullable: false)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Success = table.Column<bool>(type: "tinyint(1)", nullable: false),
                FailureKey = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                Ip = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                DeviceId = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                DeviceName = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                OperatingSystem = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true)
                    .Annotation("MySql:CharSet", "utf8mb4"),
                LoginAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_user_login_logs", x => x.Id);
            })
            .Annotation("MySql:CharSet", "utf8mb4");

        migrationBuilder.CreateIndex(
            name: "IX_user_login_logs_Ip",
            table: "user_login_logs",
            column: "Ip");

        migrationBuilder.CreateIndex(
            name: "IX_user_login_logs_LoginAt",
            table: "user_login_logs",
            column: "LoginAt");

        migrationBuilder.CreateIndex(
            name: "IX_user_login_logs_UserId",
            table: "user_login_logs",
            column: "UserId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "user_login_logs");
    }
}
