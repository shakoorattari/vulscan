import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
    ApiResponse,
    CreateProjectRequest,
    DashboardSummary,
    DiscoveryImportRequest,
    DiscoveryImportResponse,
    DiscoveryListRequest,
    DiscoveryListResponse,
    ExecutiveSummaryReport,
    InstanceDto,
    InstanceSummary,
    PackageInventory,
    PagedResult,
    ProjectDetailReport,
    ProjectDto,
    ProjectSummary,
    ProjectSummaryDto,
    ScanRun,
    ScheduleSettingsDto,
    SeverityTrend,
    TriggerScanRequest,
    TriggerScanResponse,
    UpdateInstanceRequest,
    UpdateProjectRequest,
    UpdateScheduleSettingsRequest,
    Vulnerability,
    VulnerabilityDetailReport,
    VulnerabilitySummaryItem,
} from '../models/api.models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly baseUrl = environment.apiUrl;

  constructor(private readonly http: HttpClient) {}

  // ── Dashboard ──────────────────────────────────────────────────────
  getDashboardSummary(): Observable<ApiResponse<DashboardSummary>> {
    return this.http.get<ApiResponse<DashboardSummary>>(`${this.baseUrl}/dashboard/summary`);
  }

  // ── Scans ──────────────────────────────────────────────────────────
  triggerScan(request: TriggerScanRequest): Observable<ApiResponse<TriggerScanResponse>> {
    return this.http.post<ApiResponse<TriggerScanResponse>>(`${this.baseUrl}/scans/trigger`, request);
  }

  getScanHistory(
    page = 1,
    pageSize = 25,
    projectId?: string,
  ): Observable<ApiResponse<PagedResult<ScanRun>>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (projectId !== undefined && projectId !== null) {
      params = params.set('projectId', projectId);
    }
    return this.http.get<ApiResponse<PagedResult<ScanRun>>>(`${this.baseUrl}/scans`, { params });
  }

  getScanById(id: string): Observable<ApiResponse<ScanRun>> {
    return this.http.get<ApiResponse<ScanRun>>(`${this.baseUrl}/scans/${id}`);
  }

  // ── Vulnerabilities ────────────────────────────────────────────────
  getVulnerabilities(
    filters: Record<string, string | number> = {},
  ): Observable<ApiResponse<PagedResult<Vulnerability>>> {
    let params = new HttpParams();
    Object.entries(filters).forEach(([key, value]) => {
      if (value !== null && value !== undefined && value !== '') {
        params = params.set(key, value.toString());
      }
    });
    return this.http.get<ApiResponse<PagedResult<Vulnerability>>>(`${this.baseUrl}/vulnerabilities`, {
      params,
    });
  }

  getVulnerabilityById(id: string): Observable<ApiResponse<Vulnerability>> {
    return this.http.get<ApiResponse<Vulnerability>>(`${this.baseUrl}/vulnerabilities/${id}`);
  }

  updateVulnerabilityStatus(id: string, status: string): Observable<ApiResponse<void>> {
    return this.http.patch<ApiResponse<void>>(`${this.baseUrl}/vulnerabilities/${id}/status`, { status });
  }

  // ── Health ─────────────────────────────────────────────────────────
  healthCheck(): Observable<unknown> {
    return this.http.get(`${this.baseUrl}/health`);
  }

  // ── Instances ──────────────────────────────────────────────────────
  getInstances(): Observable<ApiResponse<InstanceDto[]>> {
    return this.http.get<ApiResponse<InstanceDto[]>>(`${this.baseUrl}/instances`);
  }

  getInstanceSummaries(): Observable<ApiResponse<InstanceSummary[]>> {
    return this.http.get<ApiResponse<InstanceSummary[]>>(`${this.baseUrl}/instances/summaries`);
  }

  getInstanceById(id: string): Observable<ApiResponse<InstanceDto>> {
    return this.http.get<ApiResponse<InstanceDto>>(`${this.baseUrl}/instances/${id}`);
  }

  createInstance(): never {
    throw new Error('Instances are now created implicitly via createProject() or discoverProjects(). Use those instead.');
  }

  updateInstance(id: string, request: UpdateInstanceRequest): Observable<ApiResponse<InstanceDto>> {
    return this.http.put<ApiResponse<InstanceDto>>(`${this.baseUrl}/instances/${id}`, request);
  }

  deleteInstance(id: string): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/instances/${id}`);
  }

  testInstanceConnection(id: string): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/instances/${id}/test`, {});
  }

  // ── Projects (first-class scannable target) ────────────────────────────────
  getProjects(): Observable<ApiResponse<ProjectDto[]>> {
    return this.http.get<ApiResponse<ProjectDto[]>>(`${this.baseUrl}/projects`);
  }

  getProjectSummariesList(): Observable<ApiResponse<ProjectSummaryDto[]>> {
    return this.http.get<ApiResponse<ProjectSummaryDto[]>>(`${this.baseUrl}/projects/summaries`);
  }

  getProjectDetailById(id: string): Observable<ApiResponse<ProjectDto>> {
    return this.http.get<ApiResponse<ProjectDto>>(`${this.baseUrl}/projects/${id}`);
  }

  createProject(request: CreateProjectRequest): Observable<ApiResponse<ProjectDto>> {
    return this.http.post<ApiResponse<ProjectDto>>(`${this.baseUrl}/projects`, request);
  }

  updateProject(id: string, request: UpdateProjectRequest): Observable<ApiResponse<ProjectDto>> {
    return this.http.put<ApiResponse<ProjectDto>>(`${this.baseUrl}/projects/${id}`, request);
  }

  deleteProject(id: string): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/projects/${id}`);
  }

  enableProject(id: string): Observable<ApiResponse<ProjectDto>> {
    return this.http.post<ApiResponse<ProjectDto>>(`${this.baseUrl}/projects/${id}/enable`, {});
  }

  disableProject(id: string): Observable<ApiResponse<ProjectDto>> {
    return this.http.post<ApiResponse<ProjectDto>>(`${this.baseUrl}/projects/${id}/disable`, {});
  }

  triggerProjectScan(id: string): Observable<ApiResponse<TriggerScanResponse>> {
    return this.http.post<ApiResponse<TriggerScanResponse>>(`${this.baseUrl}/projects/${id}/scan`, {});
  }

  // ── Discovery ────────────────────────────────────────────────────────
  discoverProjects(request: DiscoveryListRequest): Observable<ApiResponse<DiscoveryListResponse>> {
    return this.http.post<ApiResponse<DiscoveryListResponse>>(`${this.baseUrl}/discovery/list`, request);
  }

  importDiscoveredProjects(request: DiscoveryImportRequest): Observable<ApiResponse<DiscoveryImportResponse>> {
    return this.http.post<ApiResponse<DiscoveryImportResponse>>(`${this.baseUrl}/discovery/import`, request);
  }

  // ── Schedule Settings (admin) ───────────────────────────────────────
  getScheduleSettings(): Observable<ApiResponse<ScheduleSettingsDto>> {
    return this.http.get<ApiResponse<ScheduleSettingsDto>>(`${this.baseUrl}/settings/schedule`);
  }

  updateScheduleSettings(request: UpdateScheduleSettingsRequest): Observable<ApiResponse<ScheduleSettingsDto>> {
    return this.http.put<ApiResponse<ScheduleSettingsDto>>(`${this.baseUrl}/settings/schedule`, request);
  }

  // ── Reports ────────────────────────────────────────────────────────
  getExecutiveSummary(scanRunId?: string): Observable<ApiResponse<ExecutiveSummaryReport>> {
    let params = new HttpParams();
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get<ApiResponse<ExecutiveSummaryReport>>(`${this.baseUrl}/reports/executive-summary`, { params });
  }

  getProjectSummaries(scanRunId?: string): Observable<ApiResponse<ProjectSummary[]>> {
    let params = new HttpParams();
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get<ApiResponse<ProjectSummary[]>>(`${this.baseUrl}/reports/projects`, { params });
  }

  getProjectReport(projectId: string, scanRunId?: string): Observable<ApiResponse<ProjectDetailReport>> {
    let params = new HttpParams();
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get<ApiResponse<ProjectDetailReport>>(`${this.baseUrl}/reports/projects/${projectId}`, { params });
  }

  getVulnerabilitySummaries(severity?: string, scanRunId?: string): Observable<ApiResponse<VulnerabilitySummaryItem[]>> {
    let params = new HttpParams();
    if (severity) params = params.set('severity', severity);
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get<ApiResponse<VulnerabilitySummaryItem[]>>(`${this.baseUrl}/reports/vulnerabilities`, { params });
  }

  getVulnerabilityReport(cveId: string, scanRunId?: string): Observable<ApiResponse<VulnerabilityDetailReport>> {
    let params = new HttpParams();
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get<ApiResponse<VulnerabilityDetailReport>>(`${this.baseUrl}/reports/vulnerabilities/${encodeURIComponent(cveId)}`, { params });
  }

  getSeverityTrends(count = 10): Observable<ApiResponse<SeverityTrend[]>> {
    const params = new HttpParams().set('count', count);
    return this.http.get<ApiResponse<SeverityTrend[]>>(`${this.baseUrl}/reports/trends`, { params });
  }

  exportProjectCsv(projectId: string, scanRunId?: string): Observable<Blob> {
    let params = new HttpParams();
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get(`${this.baseUrl}/reports/projects/${projectId}/export/csv`, {
      params, responseType: 'blob',
    });
  }

  exportProjectVulnerabilitiesCsv(projectId: string, scanRunId?: string): Observable<Blob> {
    let params = new HttpParams();
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get(`${this.baseUrl}/reports/projects/${projectId}/export/vulnerabilities-csv`, {
      params, responseType: 'blob',
    });
  }

  exportVulnerabilitiesCsv(severity?: string, scanRunId?: string): Observable<Blob> {
    let params = new HttpParams();
    if (severity) params = params.set('severity', severity);
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get(`${this.baseUrl}/reports/vulnerabilities/export/csv`, {
      params, responseType: 'blob',
    });
  }

  // ── Packages ───────────────────────────────────────────────────────
  getPackageInventory(filters: {
    scanRunId?: string;
    repositoryId?: string;
    projectId?: string;
    ecosystem?: string;
    hasVulnerabilities?: boolean;
  } = {}): Observable<ApiResponse<PackageInventory>> {
    let params = new HttpParams();
    Object.entries(filters).forEach(([k, v]) => {
      if (v !== undefined && v !== null && v !== '') {
        params = params.set(k, String(v));
      }
    });
    return this.http.get<ApiResponse<PackageInventory>>(`${this.baseUrl}/packages/inventory`, { params });
  }

  exportPackagesCsv(scanRunId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/packages/scan/${scanRunId}/csv`, { responseType: 'blob' });
  }

  downloadSbom(scanRunId: string): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/packages/scan/${scanRunId}/sbom/download`, { responseType: 'blob' });
  }
}
