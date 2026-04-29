-- ==============================================================================
-- Vulscan Database Inspection Script
-- ==============================================================================
-- Quick script to view all data in VulscanDb
-- Run this in SQL Server Management Studio, Azure Data Studio, or sqlcmd

USE VulscanDb;
GO

PRINT '==================================================================';
PRINT 'VULSCAN DATABASE SUMMARY';
PRINT '==================================================================';
PRINT '';

-- Summary counts
SELECT 
    'Azure DevOps Instances' as Entity, 
    COUNT(*) as Count 
FROM vulscan.AzureDevOpsInstances
UNION ALL
SELECT 'Projects', COUNT(*) FROM vulscan.Projects
UNION ALL
SELECT 'Repositories', COUNT(*) FROM vulscan.Repositories
UNION ALL
SELECT 'Scan Runs', COUNT(*) FROM vulscan.ScanRuns
UNION ALL
SELECT 'Discovered Packages', COUNT(*) FROM vulscan.DiscoveredPackages
UNION ALL
SELECT 'Vulnerabilities', COUNT(*) FROM vulscan.Vulnerabilities
UNION ALL
SELECT 'Users', COUNT(*) FROM vulscan.Users
UNION ALL
SELECT 'Audit Logs', COUNT(*) FROM vulscan.AuditLogs;

PRINT '';
PRINT '==================================================================';
PRINT 'AZURE DEVOPS INSTANCES';
PRINT '==================================================================';

-- View configured Azure DevOps instances
SELECT 
    Id, 
    Name, 
    Url, 
    Collection, 
    AuthMethod,
    IsEnabled,
    CreatedAt,
    UpdatedAt
FROM vulscan.AzureDevOpsInstances
ORDER BY Name;

PRINT '';
PRINT '==================================================================';
PRINT 'PROJECTS';
PRINT '==================================================================';

-- View discovered projects
SELECT 
    p.Id,
    p.Name,
    p.AzureProjectId,
    i.Name as InstanceName,
    p.DiscoveredAt,
    p.CreatedAt
FROM vulscan.Projects p
LEFT JOIN vulscan.AzureDevOpsInstances i ON p.InstanceId = i.Id
ORDER BY i.Name, p.Name;

PRINT '';
PRINT '==================================================================';
PRINT 'REPOSITORIES';
PRINT '==================================================================';

-- View repositories
SELECT 
    r.Id,
    r.Name,
    p.Name as ProjectName,
    r.DefaultBranch,
    r.CloneUrl,
    r.IsEnabled,
    r.LastScannedAt
FROM vulscan.Repositories r
LEFT JOIN vulscan.Projects p ON r.ProjectId = p.Id
ORDER BY p.Name, r.Name;

PRINT '';
PRINT '==================================================================';
PRINT 'SCAN RUNS (Last 10)';
PRINT '==================================================================';

-- View recent scan runs
SELECT TOP 10
    sr.Id,
    i.Name as InstanceName,
    sr.Status,
    sr.StartedAt,
    sr.CompletedAt,
    sr.DurationSeconds,
    sr.ReposScanned,
    sr.ReposFailed,
    sr.TotalVulnerabilities,
    sr.CriticalCount,
    sr.HighCount,
    sr.MediumCount,
    sr.LowCount
FROM vulscan.ScanRuns sr
LEFT JOIN vulscan.AzureDevOpsInstances i ON sr.InstanceId = i.Id
ORDER BY sr.StartedAt DESC;

PRINT '';
PRINT '==================================================================';
PRINT 'USERS';
PRINT '==================================================================';

-- View users
SELECT 
    Id,
    Username,
    Email,
    Role,
    IsActive,
    LastLoginAt,
    CreatedAt
FROM vulscan.Users
ORDER BY Username;

PRINT '';
PRINT '==================================================================';
PRINT 'VULNERABILITIES BY SEVERITY (if any exist)';
PRINT '==================================================================';

-- Vulnerability summary by severity
SELECT 
    Severity,
    COUNT(*) as Count,
    COUNT(DISTINCT CveId) as UniqueCVEs
FROM vulscan.Vulnerabilities
GROUP BY Severity
ORDER BY 
    CASE Severity
        WHEN 'Critical' THEN 1
        WHEN 'High' THEN 2
        WHEN 'Medium' THEN 3
        WHEN 'Low' THEN 4
        ELSE 5
    END;

PRINT '';
PRINT '==================================================================';
PRINT 'RECENT VULNERABILITIES (Top 20)';
PRINT '==================================================================';

-- Recent vulnerabilities
SELECT TOP 20
    v.Id,
    v.CveId,
    v.Severity,
    v.CvssScore,
    dp.Name as PackageName,
    dp.Version as PackageVersion,
    v.Status,
    v.DiscoveredAt
FROM vulscan.Vulnerabilities v
LEFT JOIN vulscan.DiscoveredPackages dp ON v.PackageId = dp.Id
ORDER BY v.DiscoveredAt DESC;

PRINT '';
PRINT '==================================================================';
PRINT 'PACKAGE ECOSYSTEMS (if any exist)';
PRINT '==================================================================';

-- Package distribution by ecosystem
SELECT 
    Ecosystem,
    COUNT(*) as TotalPackages,
    SUM(CASE WHEN HasVulnerabilities = 1 THEN 1 ELSE 0 END) as VulnerablePackages
FROM vulscan.DiscoveredPackages
GROUP BY Ecosystem
ORDER BY TotalPackages DESC;

PRINT '';
PRINT '==================================================================';
PRINT 'END OF REPORT';
PRINT '==================================================================';
