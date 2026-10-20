import { DecimalPipe } from '@angular/common';
import { Component, OnInit, computed, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatIconModule } from '@angular/material/icon';
import { MatInputModule } from '@angular/material/input';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatSnackBar, MatSnackBarModule } from '@angular/material/snack-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { PackageInventory } from '../../core/models/api.models';
import { ApiService } from '../../core/services/api.service';

interface InventoryScope {
  scanRunId?: string;
  projectId?: string;
  repositoryId?: string;
}

const ALL_ECOSYSTEMS = 'all';

@Component({
  selector: 'app-packages',
  standalone: true,
  imports: [
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatCardModule,
    MatChipsModule,
    MatFormFieldModule,
    MatIconModule,
    MatInputModule,
    MatPaginatorModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatSnackBarModule,
    MatTooltipModule,
  ],
  templateUrl: './packages.component.html',
  styleUrl: './packages.component.scss',
})
export class PackagesComponent implements OnInit {
  readonly allEcosystems = ALL_ECOSYSTEMS;

  readonly loading = signal(true);
  readonly errorMessage = signal('');
  readonly inventory = signal<PackageInventory | null>(null);
  readonly scope = signal<InventoryScope>({});

  // Filters are applied client-side so the ecosystem list stays stable while filtering.
  readonly search = signal('');
  readonly ecosystem = signal(ALL_ECOSYSTEMS);
  readonly vulnerableOnly = signal(false);

  readonly pageIndex = signal(0);
  readonly pageSize = signal(25);
  readonly expanded = signal<ReadonlySet<string>>(new Set());

  readonly filtered = computed(() => {
    const packages = this.inventory()?.packages ?? [];
    const term = this.search().trim().toLowerCase();
    const ecosystem = this.ecosystem();
    const vulnerableOnly = this.vulnerableOnly();

    return packages.filter(
      (p) =>
        (ecosystem === ALL_ECOSYSTEMS || p.ecosystem === ecosystem) &&
        (!vulnerableOnly || p.hasVulnerabilities) &&
        (!term ||
          [p.name, p.version, p.projectName, p.repositoryName, p.sourceFile].some((v) =>
            v?.toLowerCase().includes(term),
          )),
    );
  });

  readonly page = computed(() => {
    const start = this.pageIndex() * this.pageSize();
    return this.filtered().slice(start, start + this.pageSize());
  });

  readonly criticalTotal = computed(() => this.sumEcosystems((e) => e.criticalCount));
  readonly highTotal = computed(() => this.sumEcosystems((e) => e.highCount));

  readonly projectLabel = computed(() =>
    this.scope().projectId ? (this.inventory()?.packages[0]?.projectName ?? 'Selected project') : null,
  );

  // Exports are per scan run; the API only reports a scan run id when one was resolved.
  readonly exportScanRunId = computed(() => this.inventory()?.scanRunId ?? null);

  constructor(
    private readonly apiService: ApiService,
    private readonly snackBar: MatSnackBar,
    private readonly route: ActivatedRoute,
    private readonly router: Router,
  ) {}

  ngOnInit(): void {
    this.route.queryParamMap.subscribe((params) => {
      this.scope.set({
        scanRunId: params.get('scanRunId') || undefined,
        projectId: params.get('projectId') || undefined,
        repositoryId: params.get('repositoryId') || undefined,
      });
      this.pageIndex.set(0);
      this.expanded.set(new Set());
      this.loadInventory();
    });
  }

  loadInventory(): void {
    this.loading.set(true);
    this.errorMessage.set('');

    this.apiService.getPackageInventory(this.scope()).subscribe({
      next: (response) => {
        this.loading.set(false);
        if (response.success && response.data) {
          this.inventory.set(response.data);
        } else {
          this.inventory.set(null);
          this.errorMessage.set(response.message ?? 'Failed to load package inventory.');
        }
      },
      error: () => {
        this.loading.set(false);
        this.inventory.set(null);
        this.errorMessage.set('Failed to load package inventory.');
        this.snackBar.open('Failed to load package inventory.', 'Close', { duration: 5000 });
      },
    });
  }

  setSearch(value: string): void {
    this.search.set(value);
    this.pageIndex.set(0);
  }

  setEcosystem(value: string): void {
    this.ecosystem.set(value);
    this.pageIndex.set(0);
  }

  setVulnerableOnly(value: boolean): void {
    this.vulnerableOnly.set(value);
    this.pageIndex.set(0);
  }

  onPageChange(event: PageEvent): void {
    this.pageIndex.set(event.pageIndex);
    this.pageSize.set(event.pageSize);
  }

  isExpanded(id: string): boolean {
    return this.expanded().has(id);
  }

  toggleExpanded(id: string): void {
    const next = new Set(this.expanded());
    if (!next.delete(id)) next.add(id);
    this.expanded.set(next);
  }

  clearScope(): void {
    this.router.navigate([], { relativeTo: this.route, queryParams: {} });
  }

  exportCsv(): void {
    const scanRunId = this.exportScanRunId();
    if (!scanRunId) return;
    this.apiService.exportPackagesCsv(scanRunId).subscribe({
      next: (blob) => this.downloadBlob(blob, `packages-scan-${scanRunId}.csv`),
      error: () => this.snackBar.open('Failed to export packages.', 'Close', { duration: 5000 }),
    });
  }

  downloadSbom(): void {
    const scanRunId = this.exportScanRunId();
    if (!scanRunId) return;
    this.apiService.downloadSbom(scanRunId).subscribe({
      next: (blob) => this.downloadBlob(blob, `sbom-scan-${scanRunId}.json`),
      error: () => this.snackBar.open('Failed to download SBOM.', 'Close', { duration: 5000 }),
    });
  }

  private sumEcosystems(pick: (e: PackageInventory['ecosystems'][number]) => number): number {
    return (this.inventory()?.ecosystems ?? []).reduce((sum, e) => sum + pick(e), 0);
  }

  private downloadBlob(blob: Blob, filename: string): void {
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }
}
