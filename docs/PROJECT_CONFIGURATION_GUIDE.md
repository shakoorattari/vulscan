# Project Configuration and Branch Management Guide

## Overview

Vulscan now supports granular configuration of repositories and branches for vulnerability scanning. This feature allows administrators to:

- **Configure multiple branches per repository** for scanning
- **Enable/disable specific repositories** within a project
- **Enable/disable specific branches** for scanning
- **Track scan history per branch**
- **Scan multiple branches in parallel** during a single scan run

This provides flexibility to target different development branches (e.g., main, develop, release branches) and get comprehensive vulnerability coverage across your codebase.

## Key Concepts

### Projects
- A **Project** represents an Azure DevOps project and is the top-level scannable entity
- Each project can have multiple repositories
- Projects can have a default branch setting that applies to all repositories

### Repositories
- A **Repository** belongs to a project and represents a Git repository
- Each repository has a default branch (typically "main" or "master")
- Repositories can be enabled or disabled for scanning
- Repositories can have multiple configured branches for scanning

### Configured Branches
- A **Configured Branch** is a specific branch within a repository that has been explicitly configured for scanning
- Each branch can be independently enabled or disabled
- The system tracks scan history (count, last scanned date) per branch
- If no branches are configured, the repository's default branch is scanned

## Configuration Workflow

### 1. Access Project Configuration

From the **Reports** page:
1. Locate the project you want to configure
2. Click the **Configure** (⚙️) button in the Actions column
3. You'll be taken to the Project Configuration page

Alternatively, navigate to: `/reports/projects/{projectId}/config`

### 2. View Configuration Summary

The configuration page displays:
- **Total Repositories**: Number of repositories in the project
- **Enabled Repositories**: Number of repositories enabled for scanning
- **Total Configured Branches**: Total branches configured across all repositories
- **Project Default Branch**: The project-level default branch setting

### 3. Configure Repositories

Each repository in the project is displayed in an expansion panel showing:

#### Repository Information
- Repository name
- Clone URL
- Default branch
- Enabled/disabled status
- Number of configured branches
- Last scan date and commit

#### Repository Actions
- **Enable/Disable Repository**: Toggle whether the repository is scanned
  - Disabled repositories are completely skipped during scans
  - This is useful for archived or legacy repositories

### 4. Configure Branches

For each repository, you can configure specific branches to scan:

#### Add a Branch
1. Expand the repository panel
2. Click **Add Branch** button
3. Enter the branch name (e.g., `develop`, `release/v1.0`, `feature/new-feature`)
4. Click **Add**
5. The branch is added and enabled by default

#### Manage Branches
For each configured branch, you can:
- **Enable/Disable**: Toggle the branch scanning on/off
- **View Statistics**: See scan count and last scanned date
- **Remove Branch**: Delete the branch configuration

#### Branch Table Columns
- **Branch Name**: The name of the branch
- **Status**: Enabled or Disabled
- **Scans**: Number of times this branch has been scanned
- **Last Scanned**: Date and time of the last scan
- **Actions**: Enable/disable or remove the branch

## Scanning Behavior

### Default Behavior (No Configured Branches)
When no branches are configured for a repository:
- The system uses the **default branch** for scanning
- Default branch resolution order:
  1. Project-level default branch (if set)
  2. Repository-level default branch

### With Configured Branches
When branches are configured for a repository:
- **Only enabled configured branches are scanned**
- The default branch is **not scanned** unless it's explicitly added to the configured branches
- Multiple branches are scanned sequentially during a single scan run
- Each branch generates its own SBOM and vulnerability records

### Branch Tracking
The system tracks which branch was scanned for each:
- **SBOM (Software Bill of Materials)**: Each SBOM is associated with a specific branch
- **Vulnerability**: Each vulnerability record includes the branch where it was detected
- **Scan Statistics**: The system tracks per-branch scan count and last scanned date

## Use Cases

### Example 1: Scan Multiple Environment Branches
Configure branches for different environments:
- `main` - Production code
- `develop` - Development branch
- `staging` - Staging environment
- `release/v1.0` - Release branch

This ensures vulnerabilities are caught across all active branches.

### Example 2: Focus on Active Branches
For repositories with many stale branches:
1. Add only the active branches (e.g., `main`, `develop`)
2. Disable the repository's default branch if it's inactive
3. This reduces scan time and focuses on relevant code

### Example 3: Exclude Legacy Repositories
For projects with archived or legacy repositories:
1. Navigate to the project configuration
2. Disable the legacy repositories
3. They will be skipped in future scans

## API Reference

### Get Project Configuration
```http
GET /api/v1/projects/{projectId}/configuration
```

Returns the full project configuration including all repositories and their configured branches.

**Response:**
```json
{
  "success": true,
  "data": {
    "id": "guid",
    "name": "Project Name",
    "url": "https://...",
    "isEnabled": true,
    "defaultBranch": "main",
    "repositories": [
      {
        "id": "guid",
        "name": "RepoName",
        "defaultBranch": "main",
        "isEnabled": true,
        "configuredBranches": [
          {
            "id": "guid",
            "branchName": "develop",
            "isEnabled": true,
            "scanCount": 5,
            "lastScannedAt": "2026-05-01T10:30:00Z"
          }
        ]
      }
    ]
  }
}
```

### Update Repository
```http
PUT /api/v1/repositories/{repositoryId}
```

**Request Body:**
```json
{
  "isEnabled": true,
  "defaultBranch": "main"
}
```

### Add Branch Configuration
```http
POST /api/v1/repositories/{repositoryId}/branches
```

**Request Body:**
```json
{
  "branchName": "develop",
  "isEnabled": true
}
```

### Update Branch Configuration
```http
PUT /api/v1/repositories/{repositoryId}/branches/{branchId}
```

**Request Body:**
```json
{
  "isEnabled": false
}
```

### Delete Branch Configuration
```http
DELETE /api/v1/repositories/{repositoryId}/branches/{branchId}
```

Removes the branch from the configured branches list.

## Database Schema

### New Entities

#### RepositoryBranch
Stores configured branches for repositories.

```csharp
public class RepositoryBranch
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string BranchName { get; set; }
    public bool IsEnabled { get; set; }
    public string? LastScannedCommit { get; set; }
    public DateTime? LastScannedAt { get; set; }
    public int ScanCount { get; set; }
    public DateTime CreatedAt { get; set; }
}
```

### Updated Entities

#### Repository
- Added `ConfiguredBranches` navigation property

#### ScanRun
- Added `BranchesScanned` field to track total branches scanned

#### Sbom
- Added `BranchName` field to track which branch was scanned

#### Vulnerability
- Added `BranchName` field to track which branch the vulnerability was found in

## Migration

To apply the database schema changes:

```bash
cd server/src/Vulscan.Infrastructure
dotnet ef migrations add AddRepositoryBranchConfiguration --startup-project ../Vulscan.Api
dotnet ef database update --startup-project ../Vulscan.Api
```

## Best Practices

### 1. Start with Default Branches
- Initially, use default branch scanning to establish a baseline
- Add specific branches only when needed

### 2. Configure Active Branches Only
- Don't configure every branch in the repository
- Focus on long-lived branches (main, develop, staging, etc.)
- Avoid scanning feature branches unless they're long-running

### 3. Regular Review
- Periodically review configured branches
- Remove branches that have been merged or deleted
- Disable repositories that are no longer active

### 4. Naming Conventions
- Use consistent branch naming across repositories
- This makes configuration easier and more predictable
- Example: `main`, `develop`, `staging`, `release/*`

### 5. Monitor Scan Duration
- More configured branches = longer scan times
- Balance thoroughness with scan performance
- Consider scheduling scans during off-peak hours

## Troubleshooting

### Branch Not Found Errors
**Problem**: Scan fails with "branch not found" error

**Solution**:
1. Verify the branch name is correct (case-sensitive)
2. Check if the branch exists in Azure DevOps
3. Remove or disable the branch configuration if it no longer exists

### Long Scan Times
**Problem**: Scans take too long to complete

**Solution**:
1. Review configured branches and remove unnecessary ones
2. Disable inactive repositories
3. Consider splitting large projects into smaller scans

### Missing Vulnerabilities
**Problem**: Expected vulnerabilities not appearing

**Solution**:
1. Check if the repository is enabled
2. Verify the branch containing the vulnerability is configured and enabled
3. Review scan logs to ensure the branch was actually scanned

### Duplicate Vulnerabilities
**Problem**: Same vulnerability appears multiple times

**Solution**:
- This is expected behavior when the same vulnerability exists in multiple branches
- Each vulnerability record includes the branch name for filtering
- Use branch filters in reports to view vulnerabilities per branch

## Future Enhancements

Planned improvements for this feature:
- **Branch Auto-Discovery**: Automatically detect and suggest branches to configure
- **Branch Patterns**: Support wildcard patterns (e.g., `release/*`)
- **Differential Scanning**: Only scan branches that have changed since last scan
- **Branch Comparison**: Compare vulnerabilities across different branches
- **Notification Rules**: Configure alerts based on branch-specific findings

## Support

For issues or questions about project configuration:
- Check the application logs for detailed error messages
- Review the API documentation at `/swagger`
- Contact your system administrator or the Vulscan support team

---

**Last Updated**: May 1, 2026  
**Version**: 1.0  
**Feature Status**: Production Ready
