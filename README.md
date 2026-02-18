# 🛡️ Vulscan — Vulnerability Scanning Platform

A comprehensive vulnerability scanning and management platform for Azure DevOps repositories. Vulscan automatically discovers repositories, generates Software Bill of Materials (SBOM), and identifies security vulnerabilities across your codebase.

## 🌟 Features

### ✅ Core Capabilities

- **Automated Repository Discovery** — Scans Azure DevOps instances to discover all projects and repositories
- **SBOM Generation** — Creates CycloneDX-compliant Software Bill of Materials for each repository
- **Multi-Ecosystem Support** — Supports npm (Node.js) and NuGet (.NET) package ecosystems
- **Vulnerability Detection** — Identifies known vulnerabilities in discovered packages
- **Comprehensive Reporting** — Per-project, per-vulnerability, and executive summary reports
- **Severity Classification** — Critical, High, Medium, Low vulnerability tracking with CVSS scoring
- **CSV Export** — Machine-readable reports for packages and vulnerabilities

### 📊 Dashboard & Analytics

- Executive dashboard with KPI cards and vulnerability breakdowns
- Scan history with status tracking
- Ecosystem breakdown statistics
- Severity trend analysis across scans
- Drill-down reports for projects and CVEs

### 🔐 Security & Authentication

- JWT-based authentication
- Role-based access control (Admin/User)
- Secure PAT storage for Azure DevOps instances

## 🏗️ Architecture

### Backend

- **.NET 10 + ASP.NET Core Web API** — High-performance REST API
- **Clean Architecture** — Domain-driven design with separation of concerns
- **SQLite Database** — Lightweight with Entity Framework Core
- **Serilog** — Structured logging to console and file

### Frontend

- **Angular 19+** — Modern SPA with standalone components
- **Angular Material** — Responsive Material Design UI
- **RxJS** — Reactive state management
- **Lazy Loading** — Optimized bundle sizes with route-based code splitting

### Project Structure

```text
vulscan/
├── server/               # .NET 10 Web API
│   └── src/
│       ├── Vulscan.Api/           # API controllers & startup
│       ├── Vulscan.Application/   # Services & DTOs
│       ├── Vulscan.Domain/        # Entities & domain logic
│       └── Vulscan.Infrastructure/ # Data access & external services
├── client/               # Angular 19+ SPA
│   └── src/
│       └── app/
│           ├── core/              # Auth, guards, services, models
│           ├── features/          # Dashboard, Scans, Reports
│           └── shared/            # Layout, shared components
└── docs/                 # Documentation & BRD
```

## 🚀 Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Node.js 18+](https://nodejs.org/)
- [Angular CLI](https://angular.io/cli) (`npm install -g @angular/cli`)

### Backend Setup

```bash
cd server
dotnet restore
dotnet build
dotnet run --project src/Vulscan.Api/Vulscan.Api.csproj --urls "http://localhost:5000"
```

The API will be available at `http://localhost:5000`

**Default Admin Credentials:**

- Username: `admin`
- Password: `Admin@123!`

### Frontend Setup

```bash
cd client
npm install
npm start
```

The dashboard will be available at `http://localhost:4200`

## 📖 API Endpoints

### Authentication

- `POST /api/v1/auth/login` — Login with username/password

### Dashboard

- `GET /api/v1/dashboard/summary` — Executive dashboard summary

### Scans

- `POST /api/v1/scans/trigger` — Trigger a new scan
- `GET /api/v1/scans` — Scan history (paginated)
- `GET /api/v1/scans/{id}` — Scan details

### Vulnerabilities

- `GET /api/v1/vulnerabilities` — All vulnerabilities (paginated, filterable)
- `GET /api/v1/vulnerabilities/{id}` — Vulnerability details
- `PATCH /api/v1/vulnerabilities/{id}/status` — Update status

### Reports

- `GET /api/v1/reports/executive-summary` — Full executive report
- `GET /api/v1/reports/projects` — All project summaries
- `GET /api/v1/reports/projects/{id}` — Detailed project report
- `GET /api/v1/reports/projects/{id}/export/csv` — Export project packages
- `GET /api/v1/reports/vulnerabilities` — All CVE summaries
- `GET /api/v1/reports/vulnerabilities/{cveId}` — CVE impact report
- `GET /api/v1/reports/trends` — Severity trends across scans

### Instances

- `GET /api/v1/instances` — Azure DevOps instances
- `POST /api/v1/instances` — Register new instance
- `PUT /api/v1/instances/{id}` — Update instance
- `DELETE /api/v1/instances/{id}` — Remove instance

## 🗄️ Database Schema

### Core Entities

- **User** — Authentication and authorization
- **AzureDevOpsInstance** — Configured ADO instances
- **Project** — ADO projects
- **Repository** — Git repositories
- **ScanRun** — Scan execution metadata
- **Sbom** — Software Bill of Materials
- **DiscoveredPackage** — Packages found in repositories
- **Vulnerability** — Detected vulnerabilities
- **AuditLog** — System audit trail

## 📊 Current Status

### Scan Statistics (as of last scan)

- **94 vulnerabilities** detected (4 Critical, 32 High, 58 Medium, 0 Low)
- **65 repositories** scanned
- **28,885 packages** analyzed
- **42 projects** with vulnerabilities

### Supported Ecosystems

- **npm** — Node.js packages
- **NuGet** — .NET packages

## 🔧 Configuration

Backend configuration is in `server/src/Vulscan.Api/appsettings.json`:

```json
{
  "Jwt": {
    "SecretKey": "your-secret-key-min-32-characters",
    "Issuer": "VulscanApi",
    "Audience": "VulscanDashboard",
    "ExpiryHours": 8
  },
  "ConnectionStrings": {
    "DefaultConnection": "Data Source=vulscan.db"
  }
}
```

Frontend configuration is in `client/src/environments/environment.ts`:

```typescript
export const environment = {
  production: false,
  apiUrl: 'http://localhost:5000/api/v1'
};
```

## 📝 Documentation

Additional documentation is available in the `/docs` directory:

- `brd-v03.md` — Business Requirements Document
- `work-items/` — Detailed progress tracking for each feature area

## 🛠️ Technology Stack

**Backend:**

- .NET 10
- ASP.NET Core Web API
- Entity Framework Core
- SQLite
- Serilog
- BCrypt.Net-Next
- JWT Authentication

**Frontend:**

- Angular 19+
- Angular Material
- RxJS
- TypeScript
- SCSS

## 🚧 Roadmap

### In Progress

- Enhanced vulnerability database integration (NVD, OSV)
- HTML report generation
- Email notifications via SMTP
- Microsoft Teams webhook integration

### Planned

- Trivy/Grype integration for advanced scanning
- Scheduled scans with cron expressions
- Multi-tenancy support
- Vulnerability remediation tracking
- RBAC enhancements

## 📄 License

This project is proprietary software developed for internal use.

## 👥 Contributing

This is an internal project. For questions or issues, please contact the development team.

## 🔗 Links

- [Business Requirements Document](docs/brd-v03.md)
- [Work Items Tracking](docs/work-items/)
- [Azure DevOps](https://dev.azure.com/)

---

## Built with ❤️ for security-conscious development teams
