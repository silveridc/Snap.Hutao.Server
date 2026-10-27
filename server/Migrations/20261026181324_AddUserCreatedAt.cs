using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace server.Migrations;

/// <inheritdoc />
public partial class AddUserCreatedAt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<long>(
            name: "CreatedAt",
            table: "AspNetUsers",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "CreatedAt",
            table: "AspNetUsers");
    }
}
