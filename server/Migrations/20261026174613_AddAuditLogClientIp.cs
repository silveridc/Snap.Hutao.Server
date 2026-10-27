using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations;

/// <inheritdoc />
public partial class AddAuditLogClientIp : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Ip",
            table: "admin_audit_logs",
            type: "varchar(64)",
            maxLength: 64,
            nullable: true)
            .Annotation("MySql:CharSet", "utf8mb4");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Ip",
            table: "admin_audit_logs");
    }
}
