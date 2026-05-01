export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresAt: string;
  user: UserInfo;
}

export interface UserInfo {
  id: string;
  username: string;
  email: string;
  role: string;
}

export interface ApiResponse<T> {
  success: boolean;
  message?: string;
  data?: T;
  errors?: string[];
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface DashboardSummary {
  totalRepositories: number;
  totalScans: number;
  totalVulnerabilities: number;
  criticalCount: number;
  highCount: number;
  mediumCount: number;
  lowCount: number;
  negligibleCount: number;
  lastScanDate?: string;
  lastScanStatus?: string;
  recentScans: RecentScan[];
  topVulnerableRepos: TopVulnerableRepo[];
}

export interface RecentScan {
  id: string;
  startedAt: string;
  completedAt?: string;
  status: string;
  reposScanned: number;
  totalVulnerabilities: number;
  triggeredBy?: string;
}

export interface TopVulnerableRepo {
  repositoryId: string;
  repositoryName: string;
  projectName: string;
  criticalCount: number;
  highCount: number;
  totalVulnerabilities: number;
}

export interface ScanRun {
  id: string;
  projectId?: string;
  projectName?: string;
  startedAt: string;
  completedAt?: string;
  durationSeconds: number;
  status: string;
  reposScanned: number;
  reposFailed: number;
  totalVulnerabilities: number;
  criticalCount: number;
  highCount: number;
  mediumCount: number;
  lowCount: number;
  triggeredBy?: string;
  errorLog?: string;
}

export interface TriggerScanRequest {
  projectId: string;
}

export interface TriggerScanResponse {
  scanRunId: string;
  status: string;
  message: string;
}

export interface Vulnerability {
  id: string;
  cveId: string;
  packageName: string;
  installedVersion: string;
  fixedVersion?: string;
  severity: string;
  cvssScore?: number;
  description?: string;
  status: string;
  repositoryName: string;
  projectName: string;
  firstDetectedAt: string;
  resolvedAt?: string;
  ageDays?: number;
}

// Instance Management
export interface UpdateInstanceRequest {
  name: string;
  username?: string;
  password?: string;
  isEnabled: boolean;
}

export interface InstanceDto {
  id: string;
  name: string;
  url: string;
  collection: string;
  authMethod: string;
  isEnabled: boolean;
  hasSharedCredentials: boolean;
  projectCount: number;
  createdAt: string;
}

export interface InstanceSummary {
  id: string;
  name: string;
  url: string;
  collection: string;
  isEnabled: boolean;
}

// Project Management (first-class scannable target)
export interface CreateProjectRequest {
  name: string;
  projectUrl: string;
  username: string;
  password: string;
  defaultBranch?: string;
  cronExpression?: string;
}

export interface UpdateProjectRequest {
  name: string;
  username?: string;
  password?: string;
  defaultBranch?: string;
  cronExpression?: string;
  isEnabled: boolean;
  ownerName?: string;
  ownerEmail?: string;
  ccEmails?: string;
  sendEmailNotifications?: boolean;
}

export interface ProjectDto {
  id: string;
  name: string;
  url: string;
  defaultBranch?: string;
  isEnabled: boolean;
  hasOwnCredentials: boolean;
  instanceId: string;
  instanceName: string;
  instanceUrl: string;
  collection: string;
  azureProjectId: string;
  repositoryCount: number;
  createdAt: string;
  lastScannedAt?: string;
  totalScans: number;
  lastScanId?: string;
  lastScanStatus?: string;
  lastScanDurationSeconds?: number;
  lastScanCriticalCount: number;
  lastScanHighCount: number;
  lastScanMediumCount: number;
  lastScanLowCount: number;
  lastScanTotalVulnerabilities: number;
  cronExpression?: string;
  effectiveCron?: string;
  ownerName?: string;
  ownerEmail?: string;
  ccEmails?: string;
  sendEmailNotifications?: boolean;
}

export interface ProjectSummaryDto {
  id: string;
  name: string;
  url: string;
  isEnabled: boolean;
}

// Schedule Settings (admin) ─────────────────────────────────
export interface ScheduleSettingsDto {
  cronExpression: string;
  cronDescription: string;
  enabled: boolean;
  nextRunUtc?: string;
  updatedAt: string;
}

export interface UpdateScheduleSettingsRequest {
  cronExpression: string;
  enabled: boolean;
}

// Discovery (list + import) ─────────────────────────────────────
export interface DiscoveryListRequest {
  serverUrl: string;
  collection: string;
  username: string;
  password: string;
}

export interface DiscoveredProject {
  id: string;
  name: string;
  description?: string;
  alreadyImported: boolean;
}

export interface DiscoveryListResponse {
  instanceId: string;
  serverUrl: string;
  collection: string;
  projects: DiscoveredProject[];
}

export interface DiscoveryImportRequest {
  instanceId: string;
  azureProjectIds: string[];
  defaultBranch?: string;
}

export interface DiscoveryImportResponse {
  imported: number;
  skipped: number;
  projectIds: string[];
}

// ── Report Models ────────────────────────────────────────────────

export interface ExecutiveSummaryReport {
  generatedAt: string;
  totalProjects: number;
  totalRepositories: number;
  totalScans: number;
  totalPackages: number;
  totalVulnerabilities: number;
  criticalCount: number;
  highCount: number;
  mediumCount: number;
  lowCount: number;
  lastScanDate?: string;
  lastScanDurationSeconds?: number;
  ecosystemBreakdown: EcosystemBreakdown[];
  projectSummaries: ProjectSummary[];
  severityTrend: SeverityTrend[];
}

export interface EcosystemBreakdown {
  ecosystem: string;
  totalPackages: number;
  uniquePackages: number;
  vulnerablePackages: number;
}

export interface ProjectSummary {
  projectId: string;
  projectName: string;
  repositoryCount: number;
  totalPackages: number;
  totalVulnerabilities: number;
  criticalCount: number;
  highCount: number;
  mediumCount: number;
  lowCount: number;
}

export interface ProjectDetailReport {
  projectId: string;
  projectName: string;
  generatedAt: string;
  totalRepositories: number;
  totalPackages: number;
  totalVulnerabilities: number;
  criticalCount: number;
  highCount: number;
  mediumCount: number;
  lowCount: number;
  ecosystemBreakdown: EcosystemBreakdown[];
  repositories: RepositoryReport[];
}

export interface RepositoryReport {
  repositoryId: string;
  repositoryName: string;
  totalPackages: number;
  vulnerablePackages: number;
  vulnerabilities: ReportVulnerability[];
  topPackages: ReportPackage[];
}

export interface ReportVulnerability {
  id: string;
  cveId: string;
  packageName: string;
  installedVersion: string;
  fixedVersion?: string;
  severity: string;
  cvssScore?: number;
  description?: string;
  status: string;
  firstDetectedAt: string;
  ageDays: number;
}

export interface ReportPackage {
  ecosystem: string;
  name: string;
  version: string;
  sourceFile?: string;
  hasVulnerabilities: boolean;
  purl?: string;
}

export interface VulnerabilityDetailReport {
  cveId: string;
  severity: string;
  cvssScore?: number;
  description?: string;
  generatedAt: string;
  affectedRepositories: number;
  affectedProjects: number;
  totalOccurrences: number;
  repositories: AffectedRepository[];
}

export interface AffectedRepository {
  repositoryId: string;
  repositoryName: string;
  projectName: string;
  packageName: string;
  installedVersion: string;
  fixedVersion?: string;
  status: string;
  firstDetectedAt: string;
}

export interface VulnerabilitySummaryItem {
  cveId: string;
  severity: string;
  cvssScore?: number;
  packageName: string;
  description?: string;
  affectedRepositories: number;
  totalOccurrences: number;
  fixedVersion?: string;
}

export interface SeverityTrend {
  scanDate: string;
  scanId: string;
  critical: number;
  high: number;
  medium: number;
  low: number;
  total: number;
}

// ── Packages ─────────────────────────────────────────────────────────
export interface PackageVulnerability {
  cveId: string;
  severity: string;
  cvssScore?: number;
  fixedVersion?: string;
}

export interface PackageItem {
  id: string;
  scanRunId: string;
  repositoryId: string;
  projectId: string;
  projectName: string;
  repositoryName: string;
  ecosystem: string;
  name: string;
  version: string;
  sourceFile: string;
  isDirect: boolean;
  hasVulnerabilities: boolean;
  license?: string | null;
  purl?: string | null;
  criticalCount: number;
  highCount: number;
  mediumCount: number;
  lowCount: number;
  totalVulnerabilities: number;
  vulnerabilities: PackageVulnerability[];
}

export interface EcosystemGroupSummary {
  ecosystem: string;
  totalPackages: number;
  vulnerablePackages: number;
  criticalCount: number;
  highCount: number;
  mediumCount: number;
  lowCount: number;
}

export interface PackageInventory {
  scanRunId: string | null;
  repositoryId: string | null;
  projectId: string | null;
  totalPackages: number;
  vulnerablePackages: number;
  ecosystems: EcosystemGroupSummary[];
  packages: PackageItem[];
}

// ── Repository Configuration ──────────────────────────────────────────
export interface RepositoryConfigDto {
  id: string;
  projectId: string;
  name: string;
  cloneUrl: string;
  defaultBranch: string;
  isEnabled: boolean;
  lastScannedAt?: string;
  lastScannedCommit?: string;
  configuredBranches: BranchConfigDto[];
  totalBranches: number;
  enabledBranches: number;
}

export interface BranchConfigDto {
  id: string;
  repositoryId: string;
  branchName: string;
  isEnabled: boolean;
  lastScannedAt?: string;
  lastScannedCommit?: string;
  scanCount: number;
  createdAt: string;
}

export interface AddBranchRequest {
  branchName: string;
  isEnabled: boolean;
}

export interface UpdateBranchRequest {
  isEnabled: boolean;
}

export interface UpdateRepositoryRequest {
  isEnabled: boolean;
  defaultBranch?: string;
}

export interface ProjectConfigurationDto {
  id: string;
  name: string;
  url: string;
  isEnabled: boolean;
  defaultBranch?: string;
  repositories: RepositoryConfigDto[];
  totalRepositories: number;
  totalConfiguredBranches: number;
  enabledRepositories: number;
}
