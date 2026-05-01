using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulscan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRepositoryBranchConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BranchName",
                schema: "vulscan",
                table: "Vulnerabilities",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "BranchesScanned",
                schema: "vulscan",
                table: "ScanRuns",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "BranchName",
                schema: "vulscan",
                table: "Sboms",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "RepositoryBranches",
                schema: "vulscan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RepositoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastScannedCommit = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastScannedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ScanCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RepositoryBranches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RepositoryBranches_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "vulscan",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RepositoryBranches_RepositoryId",
                schema: "vulscan",
                table: "RepositoryBranches",
                column: "RepositoryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RepositoryBranches",
                schema: "vulscan");

            migrationBuilder.DropColumn(
                name: "BranchName",
                schema: "vulscan",
                table: "Vulnerabilities");

            migrationBuilder.DropColumn(
                name: "BranchesScanned",
                schema: "vulscan",
                table: "ScanRuns");

            migrationBuilder.DropColumn(
                name: "BranchName",
                schema: "vulscan",
                table: "Sboms");
        }
    }
}
