import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
    ApiResponse,
    CreateInstanceRequest,
    DashboardSummary,
    ExecutiveSummaryReport,
    InstanceDto,
    InstanceSummary,
    PagedResult,
    ProjectDetailReport,
    ProjectSummary,
    ScanRun,
    SeverityTrend,
    TriggerScanRequest,
    TriggerScanResponse,
    UpdateInstanceRequest,
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

  getScanHistory(page = 1, pageSize = 25): Observable<ApiResponse<PagedResult<ScanRun>>> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<ApiResponse<PagedResult<ScanRun>>>(`${this.baseUrl}/scans`, { params });
  }

  getScanById(id: number): Observable<ApiResponse<ScanRun>> {
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

  getVulnerabilityById(id: number): Observable<ApiResponse<Vulnerability>> {
    return this.http.get<ApiResponse<Vulnerability>>(`${this.baseUrl}/vulnerabilities/${id}`);
  }

  updateVulnerabilityStatus(id: number, status: string): Observable<ApiResponse<void>> {
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

  getInstanceById(id: number): Observable<ApiResponse<InstanceDto>> {
    return this.http.get<ApiResponse<InstanceDto>>(`${this.baseUrl}/instances/${id}`);
  }

  createInstance(request: CreateInstanceRequest): Observable<ApiResponse<InstanceDto>> {
    return this.http.post<ApiResponse<InstanceDto>>(`${this.baseUrl}/instances`, request);
  }

  updateInstance(id: number, request: UpdateInstanceRequest): Observable<ApiResponse<InstanceDto>> {
    return this.http.put<ApiResponse<InstanceDto>>(`${this.baseUrl}/instances/${id}`, request);
  }

  deleteInstance(id: number): Observable<ApiResponse<void>> {
    return this.http.delete<ApiResponse<void>>(`${this.baseUrl}/instances/${id}`);
  }

  testInstanceConnection(id: number): Observable<ApiResponse<void>> {
    return this.http.post<ApiResponse<void>>(`${this.baseUrl}/instances/${id}/test`, {});
  }

  // ── Reports ────────────────────────────────────────────────────────
  getExecutiveSummary(scanRunId?: number): Observable<ApiResponse<ExecutiveSummaryReport>> {
    let params = new HttpParams();
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get<ApiResponse<ExecutiveSummaryReport>>(`${this.baseUrl}/reports/executive-summary`, { params });
  }

  getProjectSummaries(scanRunId?: number): Observable<ApiResponse<ProjectSummary[]>> {
    let params = new HttpParams();
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get<ApiResponse<ProjectSummary[]>>(`${this.baseUrl}/reports/projects`, { params });
  }

  getProjectReport(projectId: number, scanRunId?: number): Observable<ApiResponse<ProjectDetailReport>> {
    let params = new HttpParams();
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get<ApiResponse<ProjectDetailReport>>(`${this.baseUrl}/reports/projects/${projectId}`, { params });
  }

  getVulnerabilitySummaries(severity?: string, scanRunId?: number): Observable<ApiResponse<VulnerabilitySummaryItem[]>> {
    let params = new HttpParams();
    if (severity) params = params.set('severity', severity);
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get<ApiResponse<VulnerabilitySummaryItem[]>>(`${this.baseUrl}/reports/vulnerabilities`, { params });
  }

  getVulnerabilityReport(cveId: string, scanRunId?: number): Observable<ApiResponse<VulnerabilityDetailReport>> {
    let params = new HttpParams();
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get<ApiResponse<VulnerabilityDetailReport>>(`${this.baseUrl}/reports/vulnerabilities/${encodeURIComponent(cveId)}`, { params });
  }

  getSeverityTrends(count = 10): Observable<ApiResponse<SeverityTrend[]>> {
    const params = new HttpParams().set('count', count);
    return this.http.get<ApiResponse<SeverityTrend[]>>(`${this.baseUrl}/reports/trends`, { params });
  }

  exportProjectCsv(projectId: number, scanRunId?: number): Observable<Blob> {
    let params = new HttpParams();
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get(`${this.baseUrl}/reports/projects/${projectId}/export/csv`, {
      params, responseType: 'blob',
    });
  }

  exportProjectVulnerabilitiesCsv(projectId: number, scanRunId?: number): Observable<Blob> {
    let params = new HttpParams();
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get(`${this.baseUrl}/reports/projects/${projectId}/export/vulnerabilities-csv`, {
      params, responseType: 'blob',
    });
  }

  exportVulnerabilitiesCsv(severity?: string, scanRunId?: number): Observable<Blob> {
    let params = new HttpParams();
    if (severity) params = params.set('severity', severity);
    if (scanRunId) params = params.set('scanRunId', scanRunId);
    return this.http.get(`${this.baseUrl}/reports/vulnerabilities/export/csv`, {
      params, responseType: 'blob',
    });
  }
}
