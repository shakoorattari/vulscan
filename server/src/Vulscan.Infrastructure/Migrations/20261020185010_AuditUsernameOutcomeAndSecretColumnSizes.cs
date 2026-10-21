using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulscan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuditUsernameOutcomeAndSecretColumnSizes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AuditLogs_Users_UserId",
                schema: "vulscan",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "CredentialReference",
                schema: "vulscan",
                table: "Projects",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CredentialReference",
                schema: "vulscan",
                table: "AzureDevOpsInstances",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "EntityId",
                schema: "vulscan",
                table: "AuditLogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Outcome",
                schema: "vulscan",
                table: "AuditLogs",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Success");

            migrationBuilder.AddColumn<string>(
                name: "Username",
                schema: "vulscan",
                table: "AuditLogs",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Action",
                schema: "vulscan",
                table: "AuditLogs",
                column: "Action");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AuditLogs_Action",
                schema: "vulscan",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Outcome",
                schema: "vulscan",
                table: "AuditLogs");

            migrationBuilder.DropColumn(
                name: "Username",
                schema: "vulscan",
                table: "AuditLogs");

            migrationBuilder.AlterColumn<string>(
                name: "CredentialReference",
                schema: "vulscan",
                table: "Projects",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CredentialReference",
                schema: "vulscan",
                table: "AzureDevOpsInstances",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(2000)",
                oldMaxLength: 2000);

            migrationBuilder.AlterColumn<string>(
                name: "EntityId",
                schema: "vulscan",
                table: "AuditLogs",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditLogs_Users_UserId",
                schema: "vulscan",
                table: "AuditLogs",
                column: "UserId",
                principalSchema: "vulscan",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
