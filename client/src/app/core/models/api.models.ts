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
  id: number;
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
  id: number;
  startedAt: string;
  completedAt?: string;
  status: string;
  reposScanned: number;
  totalVulnerabilities: number;
  triggeredBy?: string;
}

export interface TopVulnerableRepo {
  repositoryId: number;
  repositoryName: string;
  projectName: string;
  criticalCount: number;
  highCount: number;
  totalVulnerabilities: number;
}

export interface ScanRun {
  id: number;
  instanceId?: number;
  instanceName?: string;
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
  instanceId: number;
}

export interface TriggerScanResponse {
  scanRunId: number;
  status: string;
  message: string;
}

export interface Vulnerability {
  id: number;
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
export interface CreateInstanceRequest {
  name: string;
  projectUrl: string;
  username: string;
  password: string;
  branch?: string;
}

export interface UpdateInstanceRequest {
  name: string;
  username?: string;
  password?: string;
  branch?: string;
  isEnabled: boolean;
}

export interface InstanceDto {
  id: number;
  name: string;
  url: string;
  collection: string;
  projectName: string;
  authMethod: string;
  isEnabled: boolean;
  createdAt: string;
  lastScannedAt?: string;
  totalScans: number;
  totalVulnerabilities: number;
}

export interface InstanceSummary {
  id: number;
  name: string;
  projectName: string;
  isEnabled: boolean;
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
  projectId: number;
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
  projectId: number;
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
  repositoryId: number;
  repositoryName: string;
  totalPackages: number;
  vulnerablePackages: number;
  vulnerabilities: ReportVulnerability[];
  topPackages: ReportPackage[];
}

export interface ReportVulnerability {
  id: number;
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
  repositoryId: number;
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
  scanId: number;
  critical: number;
  high: number;
  medium: number;
  low: number;
  total: number;
}
