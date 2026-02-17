# ⏰ Scheduling & Automation — Work Items

| ID | Requirement | Priority | Status | Notes |
|---|---|---|---|---|
| **FR-SCHED-001** | APScheduler integration | 🟠 High | 🔶 **Alt. Approach** | `ScanBackgroundWorker` polls every 5s for queued scans |
| **FR-SCHED-002** | Windows Task Scheduler support | 🟠 High | ❌ **Not Done** | |
| **FR-SCHED-003** | Configurable scan frequency (cron) | 🔴 Critical | ❌ **Not Done** | Manual trigger only via API |
| **FR-SCHED-004** | Per-collection schedules | 🟡 Medium | ❌ **Not Done** | |
| **FR-SCHED-005** | Skip scan if no new commits | 🟡 Medium | ❌ **Not Done** | |
| **FR-SCHED-006** | Retry with exponential backoff | 🟡 Medium | ❌ **Not Done** | |
| **FR-SCHED-007** | Scan locking (prevent concurrent) | 🟠 High | ✅ **Done** | `ScanService.TriggerScanAsync` checks for running scans |
| **FR-SCHED-008** | Log schedule triggers to DB | 🟠 High | 🔶 **Partial** | Scan triggers logged via `ScanRun` entity |
