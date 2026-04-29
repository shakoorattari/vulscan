using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Vulscan.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "vulscan");

            migrationBuilder.CreateTable(
                name: "AzureDevOpsInstances",
                schema: "vulscan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Collection = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AuthMethod = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CredentialReference = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AzureDevOpsInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Users",
                schema: "vulscan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Username = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    Role = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Projects",
                schema: "vulscan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstanceId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    AzureProjectId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DiscoveredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projects_AzureDevOpsInstances_InstanceId",
                        column: x => x.InstanceId,
                        principalSchema: "vulscan",
                        principalTable: "AzureDevOpsInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                schema: "vulscan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<int>(type: "int", nullable: true),
                    Action = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<int>(type: "int", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AuditLogs_Users_UserId",
                        column: x => x.UserId,
                        principalSchema: "vulscan",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ScanRuns",
                schema: "vulscan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InstanceId = table.Column<int>(type: "int", nullable: true),
                    TriggeredByUserId = table.Column<int>(type: "int", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DurationSeconds = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ReposScanned = table.Column<int>(type: "int", nullable: false),
                    ReposFailed = table.Column<int>(type: "int", nullable: false),
                    TotalVulnerabilities = table.Column<int>(type: "int", nullable: false),
                    CriticalCount = table.Column<int>(type: "int", nullable: false),
                    HighCount = table.Column<int>(type: "int", nullable: false),
                    MediumCount = table.Column<int>(type: "int", nullable: false),
                    LowCount = table.Column<int>(type: "int", nullable: false),
                    ErrorLog = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScanRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScanRuns_AzureDevOpsInstances_InstanceId",
                        column: x => x.InstanceId,
                        principalSchema: "vulscan",
                        principalTable: "AzureDevOpsInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ScanRuns_Users_TriggeredByUserId",
                        column: x => x.TriggeredByUserId,
                        principalSchema: "vulscan",
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Repositories",
                schema: "vulscan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CloneUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DefaultBranch = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false, defaultValue: "main"),
                    LastScannedCommit = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    LastScannedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Repositories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Repositories_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalSchema: "vulscan",
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Sboms",
                schema: "vulscan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RepositoryId = table.Column<int>(type: "int", nullable: false),
                    ScanRunId = table.Column<int>(type: "int", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Generator = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ComponentCount = table.Column<int>(type: "int", nullable: false),
                    SbomJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CommitHash = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    GenerationDurationMs = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sboms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Sboms_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "vulscan",
                        principalTable: "Repositories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Sboms_ScanRuns_ScanRunId",
                        column: x => x.ScanRunId,
                        principalSchema: "vulscan",
                        principalTable: "ScanRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DiscoveredPackages",
                schema: "vulscan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ScanRunId = table.Column<int>(type: "int", nullable: false),
                    RepositoryId = table.Column<int>(type: "int", nullable: false),
                    SbomId = table.Column<int>(type: "int", nullable: true),
                    Ecosystem = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SourceFile = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    IsDirect = table.Column<bool>(type: "bit", nullable: false),
                    HasVulnerabilities = table.Column<bool>(type: "bit", nullable: false),
                    License = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PackageUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Purl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DiscoveredPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DiscoveredPackages_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "vulscan",
                        principalTable: "Repositories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DiscoveredPackages_Sboms_SbomId",
                        column: x => x.SbomId,
                        principalSchema: "vulscan",
                        principalTable: "Sboms",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DiscoveredPackages_ScanRuns_ScanRunId",
                        column: x => x.ScanRunId,
                        principalSchema: "vulscan",
                        principalTable: "ScanRuns",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Vulnerabilities",
                schema: "vulscan",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SbomId = table.Column<int>(type: "int", nullable: true),
                    ScanRunId = table.Column<int>(type: "int", nullable: false),
                    RepositoryId = table.Column<int>(type: "int", nullable: false),
                    CveId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PackageName = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    InstalledVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FixedVersion = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Severity = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CvssScore = table.Column<double>(type: "float(4)", precision: 4, scale: 2, nullable: true),
                    CvssVector = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SourceDb = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FirstDetectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vulnerabilities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vulnerabilities_Repositories_RepositoryId",
                        column: x => x.RepositoryId,
                        principalSchema: "vulscan",
                        principalTable: "Repositories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Vulnerabilities_Sboms_SbomId",
                        column: x => x.SbomId,
                        principalSchema: "vulscan",
                        principalTable: "Sboms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Vulnerabilities_ScanRuns_ScanRunId",
                        column: x => x.ScanRunId,
                        principalSchema: "vulscan",
                        principalTable: "ScanRuns",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                schema: "vulscan",
                table: "Users",
                columns: new[] { "Id", "CreatedAt", "Email", "IsActive", "LastLoginAt", "PasswordHash", "Role", "UpdatedAt", "Username" },
                values: new object[] { 1, new DateTime(2025, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "admin@vulscan.local", true, null, "$2a$12$OOF3yNNPWG8p2JnOjF4V5u83Oc..5jM7H5vvgNd7cJc5PQG.DLhVW", "Admin", null, "admin" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_Timestamp",
                schema: "vulscan",
                table: "AuditLogs",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditLogs_UserId",
                schema: "vulscan",
                table: "AuditLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AzureDevOpsInstances_Name",
                schema: "vulscan",
                table: "AzureDevOpsInstances",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredPackages_HasVulnerabilities",
                schema: "vulscan",
                table: "DiscoveredPackages",
                column: "HasVulnerabilities");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredPackages_RepositoryId_Name",
                schema: "vulscan",
                table: "DiscoveredPackages",
                columns: new[] { "RepositoryId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredPackages_SbomId",
                schema: "vulscan",
                table: "DiscoveredPackages",
                column: "SbomId");

            migrationBuilder.CreateIndex(
                name: "IX_DiscoveredPackages_ScanRunId_Ecosystem",
                schema: "vulscan",
                table: "DiscoveredPackages",
                columns: new[] { "ScanRunId", "Ecosystem" });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_InstanceId_AzureProjectId",
                schema: "vulscan",
                table: "Projects",
                columns: new[] { "InstanceId", "AzureProjectId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Repositories_ProjectId_Name",
                schema: "vulscan",
                table: "Repositories",
                columns: new[] { "ProjectId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Sboms_RepositoryId_ScanRunId",
                schema: "vulscan",
                table: "Sboms",
                columns: new[] { "RepositoryId", "ScanRunId" });

            migrationBuilder.CreateIndex(
                name: "IX_Sboms_ScanRunId",
                schema: "vulscan",
                table: "Sboms",
                column: "ScanRunId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanRuns_InstanceId",
                schema: "vulscan",
                table: "ScanRuns",
                column: "InstanceId");

            migrationBuilder.CreateIndex(
                name: "IX_ScanRuns_StartedAt",
                schema: "vulscan",
                table: "ScanRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ScanRuns_Status",
                schema: "vulscan",
                table: "ScanRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ScanRuns_TriggeredByUserId",
                schema: "vulscan",
                table: "ScanRuns",
                column: "TriggeredByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                schema: "vulscan",
                table: "Users",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                schema: "vulscan",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_CveId",
                schema: "vulscan",
                table: "Vulnerabilities",
                column: "CveId");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_PackageName",
                schema: "vulscan",
                table: "Vulnerabilities",
                column: "PackageName");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_RepositoryId_CveId_PackageName",
                schema: "vulscan",
                table: "Vulnerabilities",
                columns: new[] { "RepositoryId", "CveId", "PackageName" });

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_SbomId",
                schema: "vulscan",
                table: "Vulnerabilities",
                column: "SbomId");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_ScanRunId",
                schema: "vulscan",
                table: "Vulnerabilities",
                column: "ScanRunId");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_Severity",
                schema: "vulscan",
                table: "Vulnerabilities",
                column: "Severity");

            migrationBuilder.CreateIndex(
                name: "IX_Vulnerabilities_Status",
                schema: "vulscan",
                table: "Vulnerabilities",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditLogs",
                schema: "vulscan");

            migrationBuilder.DropTable(
                name: "DiscoveredPackages",
                schema: "vulscan");

            migrationBuilder.DropTable(
                name: "Vulnerabilities",
                schema: "vulscan");

            migrationBuilder.DropTable(
                name: "Sboms",
                schema: "vulscan");

            migrationBuilder.DropTable(
                name: "Repositories",
                schema: "vulscan");

            migrationBuilder.DropTable(
                name: "ScanRuns",
                schema: "vulscan");

            migrationBuilder.DropTable(
                name: "Projects",
                schema: "vulscan");

            migrationBuilder.DropTable(
                name: "Users",
                schema: "vulscan");

            migrationBuilder.DropTable(
                name: "AzureDevOpsInstances",
                schema: "vulscan");
        }
    }
}
