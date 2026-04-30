-- Clean all scan data, projects, repos, vulnerabilities, packages, and SBOM
-- Preserves: Users table
-- Order matters due to foreign key constraints

USE [VulscanDb];
GO

-- 1. Delete child tables first (have FKs to multiple parents)
DELETE FROM [vulscan].[Vulnerabilities];
DELETE FROM [vulscan].[DiscoveredPackages];
DELETE FROM [vulscan].[Sboms];

-- 2. Delete repositories (has FK to Projects)
DELETE FROM [vulscan].[Repositories];

-- 3. Delete projects (has FK to AzureDevOpsInstances)
DELETE FROM [vulscan].[Projects];

-- 4. Delete scan runs (has FK to AzureDevOpsInstances and Users)
DELETE FROM [vulscan].[ScanRuns];

-- 5. Delete Azure DevOps instances (parent table)
DELETE FROM [vulscan].[AzureDevOpsInstances];

-- 6. Clear audit logs (optional - has FK to Users)
DELETE FROM [vulscan].[AuditLogs];  -- Changed to plural

-- Verify counts
SELECT 'AzureDevOpsInstances' AS [Table], COUNT(*) AS [Count] FROM [vulscan].[AzureDevOpsInstances]
UNION ALL
SELECT 'Projects', COUNT(*) FROM [vulscan].[Projects]
UNION ALL
SELECT 'Repositories', COUNT(*) FROM [vulscan].[Repositories]
UNION ALL
SELECT 'ScanRuns', COUNT(*) FROM [vulscan].[ScanRuns]
UNION ALL
SELECT 'Sboms', COUNT(*) FROM [vulscan].[Sboms]
UNION ALL
SELECT 'Vulnerabilities', COUNT(*) FROM [vulscan].[Vulnerabilities]
UNION ALL
SELECT 'DiscoveredPackages', COUNT(*) FROM [vulscan].[DiscoveredPackages]
UNION ALL
SELECT 'AuditLogs', COUNT(*) FROM [vulscan].[AuditLogs]
UNION ALL
SELECT 'Users (PRESERVED)', COUNT(*) FROM [vulscan].[Users];
GO