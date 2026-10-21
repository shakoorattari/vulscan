using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulscan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserAccessLifecycleAndRefreshTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "vulscan",
                table: "Users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AddColumn<DateTime>(
                name: "AccessGrantedAt",
                schema: "vulscan",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuthProvider",
                schema: "vulscan",
                table: "Users",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Local");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeactivatedAt",
                schema: "vulscan",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeactivationReason",
                schema: "vulscan",
                table: "Users",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExternalId",
                schema: "vulscan",
                table: "Users",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginCount",
                schema: "vulscan",
                table: "Users",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAccessReviewAt",
                schema: "vulscan",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastAccessReviewBy",
                schema: "vulscan",
                table: "Users",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEndAt",
                schema: "vulscan",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                schema: "vulscan",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAt",
                schema: "vulscan",
                table: "Users",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "RefreshTokens",
                schema: "vulscan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    FamilyId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedReason = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReplacedByTokenHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    CreatedByIp = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RefreshTokens_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "vulscan",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                schema: "vulscan",
                table: "Users",
                keyColumn: "Id",
                keyValue: new Guid("00000000-0000-0000-0000-000000000001"),
                columns: new[] { "AccessGrantedAt", "DeactivatedAt", "DeactivationReason", "ExternalId", "FailedLoginCount", "LastAccessReviewAt", "LastAccessReviewBy", "LockoutEndAt", "MustChangePassword", "PasswordChangedAt" },
                values: new object[] { null, null, null, null, 0, null, null, null, true, null });

            migrationBuilder.CreateIndex(
                name: "IX_Users_ExternalId",
                schema: "vulscan",
                table: "Users",
                column: "ExternalId",
                unique: true,
                filter: "[ExternalId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_FamilyId",
                schema: "vulscan",
                table: "RefreshTokens",
                column: "FamilyId");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_TokenHash",
                schema: "vulscan",
                table: "RefreshTokens",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_RevokedAt",
                schema: "vulscan",
                table: "RefreshTokens",
                columns: new[] { "UserId", "RevokedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RefreshTokens",
                schema: "vulscan");

            migrationBuilder.DropIndex(
                name: "IX_Users_ExternalId",
                schema: "vulscan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AccessGrantedAt",
                schema: "vulscan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "AuthProvider",
                schema: "vulscan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeactivatedAt",
                schema: "vulscan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "DeactivationReason",
                schema: "vulscan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "ExternalId",
                schema: "vulscan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FailedLoginCount",
                schema: "vulscan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastAccessReviewAt",
                schema: "vulscan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastAccessReviewBy",
                schema: "vulscan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LockoutEndAt",
                schema: "vulscan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                schema: "vulscan",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAt",
                schema: "vulscan",
                table: "Users");

            migrationBuilder.AlterColumn<string>(
                name: "PasswordHash",
                schema: "vulscan",
                table: "Users",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512,
                oldNullable: true);
        }
    }
}
