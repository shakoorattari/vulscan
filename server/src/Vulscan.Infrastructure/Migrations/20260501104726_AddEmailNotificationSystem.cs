using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulscan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailNotificationSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CcEmails",
                schema: "vulscan",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerEmail",
                schema: "vulscan",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OwnerName",
                schema: "vulscan",
                table: "Projects",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "SendEmailNotifications",
                schema: "vulscan",
                table: "Projects",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "EmailLogs",
                schema: "vulscan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ToEmails = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CcEmails = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Subject = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Body = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSent = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SentAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ScanRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    TriggeredByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EmailType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    AttachmentSize = table.Column<long>(type: "bigint", nullable: true),
                    RetryCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmailLogs_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "vulscan",
                        principalTable: "Projects",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmailLogs_ScanRuns_ScanRunId",
                        column: x => x.ScanRunId,
                        principalSchema: "vulscan",
                        principalTable: "ScanRuns",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_EmailLogs_Users_TriggeredByUserId",
                        column: x => x.TriggeredByUserId,
                        principalSchema: "vulscan",
                        principalTable: "Users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SmtpConfigurations",
                schema: "vulscan",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Host = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Port = table.Column<int>(type: "int", nullable: false),
                    UseSsl = table.Column<bool>(type: "bit", nullable: false),
                    UseStartTls = table.Column<bool>(type: "bit", nullable: false),
                    Username = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Password = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FromEmail = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FromName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReplyToEmail = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    LastTestedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastTestResult = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SmtpConfigurations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_ProjectId",
                schema: "vulscan",
                table: "EmailLogs",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_ScanRunId",
                schema: "vulscan",
                table: "EmailLogs",
                column: "ScanRunId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailLogs_TriggeredByUserId",
                schema: "vulscan",
                table: "EmailLogs",
                column: "TriggeredByUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailLogs",
                schema: "vulscan");

            migrationBuilder.DropTable(
                name: "SmtpConfigurations",
                schema: "vulscan");

            migrationBuilder.DropColumn(
                name: "CcEmails",
                schema: "vulscan",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OwnerEmail",
                schema: "vulscan",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "OwnerName",
                schema: "vulscan",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "SendEmailNotifications",
                schema: "vulscan",
                table: "Projects");
        }
    }
}
