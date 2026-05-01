# 📅 Vulscan Project Plan — 2026 Roadmap

**Version:** 1.0  
**Last Updated:** May 1, 2026  
**Planning Horizon:** Q2 2026 - Q1 2027  
**Status:** Active Development

---

## 🎯 Vision & Goals

### Mission Statement

> Build the leading **on-premises vulnerability management platform** for Azure DevOps Server, empowering enterprises to secure their software supply chain with transparency, automation, and compliance.

### 2026 Goals

| Goal | Target | Current Status |
|------|--------|----------------|
| **Product Maturity** | Beta → v1.0 Production | 🟡 Alpha (60% complete) |
| **Pilot Customers** | 5 enterprises | 🔴 0 (recruiting) |
| **Feature Completeness** | Core + Advanced | 🟡 Core done, Advanced pending |
| **Open Source Release** | Community Edition live | 🔴 Not released |
| **Revenue** | First $10k ARR | 🔴 $0 |
| **Team Growth** | 3 → 5 people | 🔴 3 people |

---

## 📊 Current State Assessment

### ✅ What's Complete (As of May 1, 2026)

| Feature | Status | Quality |
|---------|--------|---------|
| OSV.dev API integration | ✅ Complete | 🟢 Production-ready |
| npm & NuGet scanning | ✅ Complete | 🟢 Production-ready |
| Azure DevOps PAT/Basic Auth | ✅ Complete | 🟢 Production-ready |
| SQLite database | ✅ Complete | 🟢 Production-ready |
| .NET 10 Web API | ✅ Complete | 🟢 Production-ready |
| Angular 19 dashboard | ✅ Complete | 🟡 Needs polish |
| JWT authentication | ✅ Complete | 🟢 Production-ready |
| Email notifications | ✅ Complete | 🟡 Basic |
| Executive reports | ✅ Complete | 🟡 Basic |
| Background scan workers | ✅ Complete | 🟢 Production-ready |

**Overall Assessment:** 60% complete for v1.0 launch

### 🔴 Critical Gaps for v1.0

| Gap | Impact | Effort |
|-----|--------|--------|
| Container image scanning | High | 2 weeks |
| License compliance | High | 2 weeks |
| Teams webhook notifications | Medium | 1 week |
| Advanced reporting (PDF) | Medium | 2 weeks |
| User management UI | High | 1 week |
| Documentation & tutorials | High | 1 week |
| Installer/deployment scripts | High | 1 week |
| Performance optimization | Medium | 1 week |

**Total Gap:** ~11 weeks of development

---

## 🗓️ Quarterly Roadmap

### Q2 2026 (May - July) — **Beta Release**

**Theme:** Complete Core Features & Launch Community Edition

#### Sprint 1: Container & License Scanning (May 1-14)

**Goals:**
- ✅ Integrate OSV Scanner CLI
- ✅ Add container scanning API
- ✅ Add license scanning API
- ✅ Database migrations for new tables

**Deliverables:**
- [ ] `OsvScannerService.cs` implementation
- [ ] `ContainerScansController.cs` with REST API
- [ ] `LicenseScansController.cs` with REST API
- [ ] EF Core migrations (ContainerScans, LicenseFindings tables)
- [ ] Unit tests for new services

**Acceptance Criteria:**
- Can scan Docker image via API
- Can scan repository for license compliance
- Results stored in database
- API documented in Swagger

---

#### Sprint 2: Dashboard Enhancements (May 15-28)

**Goals:**
- ✅ Add container scan UI
- ✅ Add license compliance dashboard
- ✅ Polish existing UI (UX improvements)
- ✅ Add dark mode

**Deliverables:**
- [ ] Container scan tab in Angular dashboard
- [ ] License compliance view with allowed/denied list
- [ ] UI polish (loading states, error handling)
- [ ] Dark mode theme
- [ ] Responsive design improvements

**Acceptance Criteria:**
- All features accessible via web UI
- Professional look & feel
- Mobile-friendly responsive design
- No broken links or console errors

---

#### Sprint 3: Notifications & Reporting (May 29 - June 11)

**Goals:**
- ✅ Add Teams webhook support
- ✅ Enhance email templates
- ✅ Add PDF report generation
- ✅ Add scheduled reporting

**Deliverables:**
- [ ] Teams webhook integration
- [ ] HTML email templates (branded)
- [ ] PDF report generation (executive summary)
- [ ] Scheduled daily/weekly reports
- [ ] Notification configuration UI

**Acceptance Criteria:**
- Teams notification on critical vulnerabilities
- Professional HTML emails
- PDF reports exportable
- Configurable notification thresholds

---

#### Sprint 4: User Management & RBAC (June 12-25)

**Goals:**
- ✅ User management UI
- ✅ Role-based access control
- ✅ Audit logging
- ✅ Password policies

**Deliverables:**
- [ ] User management dashboard (create/edit/delete)
- [ ] Role assignment UI (Admin/User/Viewer)
- [ ] Audit log table & viewer
- [ ] Password complexity enforcement
- [ ] Session management

**Acceptance Criteria:**
- Admin can manage users via UI
- Roles enforce proper access control
- All actions logged in audit trail
- Secure password requirements

---

#### Sprint 5: Documentation & Polish (June 26 - July 9)

**Goals:**
- ✅ User documentation
- ✅ Admin guide
- ✅ API documentation
- ✅ Installation scripts

**Deliverables:**
- [ ] User guide (how to use dashboard)
- [ ] Admin guide (installation, configuration)
- [ ] API reference (Swagger + Markdown)
- [ ] Windows installer (MSI or PowerShell script)
- [ ] Linux installer (Bash script)
- [ ] Video tutorials (YouTube)

**Acceptance Criteria:**
- Complete documentation published
- One-click installer works
- Video tutorials cover key workflows
- README.md is comprehensive

---

#### Sprint 6: Beta Testing & Bug Fixes (July 10-23)

**Goals:**
- ✅ Internal testing
- ✅ Fix critical bugs
- ✅ Performance optimization
- ✅ Security review

**Deliverables:**
- [ ] Load testing (1000+ repositories)
- [ ] Security audit (OWASP Top 10)
- [ ] Bug fixes (P0/P1 issues)
- [ ] Performance optimizations
- [ ] Beta release candidate

**Acceptance Criteria:**
- No P0/P1 bugs remaining
- Can handle 1000 repos
- Security scan passes
- Beta release tagged in Git

---

#### Sprint 7: Community Edition Release (July 24-31)

**Goals:**
- ✅ Open-source release
- ✅ Marketing launch
- ✅ Community onboarding

**Deliverables:**
- [ ] GitHub public repository
- [ ] License file (Apache 2.0 / MIT)
- [ ] Contributing guidelines
- [ ] Release announcement (blog, LinkedIn, Twitter)
- [ ] Product Hunt launch
- [ ] Docker Hub images

**Acceptance Criteria:**
- Public GitHub repo with 50+ stars
- 100+ downloads in first week
- Active community discussions
- No critical issues reported

**🎉 Q2 Milestone:** Community Edition Beta released

---

### Q3 2026 (August - October) — **v1.0 Production & Pilot Customers**

**Theme:** Production Hardening & Customer Acquisition

#### August (Sprints 8-9)

**Focus:** Production Readiness

**Key Deliverables:**
- [ ] SQL Server support (migrate from SQLite)
- [ ] High availability setup (load balancing)
- [ ] Backup & restore functionality
- [ ] Disaster recovery documentation
- [ ] Production deployment guide
- [ ] Monitoring & alerting (Prometheus, Grafana)

**Milestone:** v1.0 Production Release

---

#### September (Sprints 10-11)

**Focus:** Pilot Customer Onboarding

**Key Deliverables:**
- [ ] Onboard Pilot Customer #1 (enterprise)
- [ ] Onboard Pilot Customer #2 (mid-size)
- [ ] Onboard Pilot Customer #3 (government)
- [ ] Customer success playbook
- [ ] Support ticketing system
- [ ] Customer feedback loop

**Milestone:** 3 Pilot Customers Live

---

#### October (Sprints 12-13)

**Focus:** Feature Enhancements (Based on Feedback)

**Key Deliverables:**
- [ ] Guided remediation workflows
- [ ] Custom vulnerability severity rules
- [ ] Integration with CI/CD pipelines
- [ ] Slack notifications (in addition to Teams)
- [ ] Advanced filtering & search
- [ ] Export to JIRA/Azure Boards

**Milestone:** 5 Pilot Customers Live, First $10k ARR

---

### Q4 2026 (November - December) — **Professional Edition Launch**

**Theme:** Commercialization & Growth

#### November (Sprints 14-15)

**Focus:** Professional Edition Features

**Key Deliverables:**
- [ ] Multi-instance management UI
- [ ] Advanced reporting (custom templates)
- [ ] SSO/SAML authentication
- [ ] API rate limiting & quotas
- [ ] White-label branding options
- [ ] Licensing & activation system

**Milestone:** Professional Edition v1.0

---

#### December (Sprints 16-17)

**Focus:** Sales & Marketing

**Key Deliverables:**
- [ ] Pricing page & checkout flow
- [ ] Trial license system (30-day free)
- [ ] Sales collateral (deck, datasheets)
- [ ] Case studies (3 customer stories)
- [ ] Conference talks (2-3 events)
- [ ] Partner program launch

**Milestone:** $50k ARR, 10 Paying Customers

---

### Q1 2027 (January - March) — **Enterprise Edition & Scale**

**Theme:** Enterprise Features & Market Expansion

**Key Deliverables:**
- [ ] Air-gapped deployment support
- [ ] Offline vulnerability database
- [ ] Advanced RBAC (custom roles)
- [ ] Multi-region deployment
- [ ] 24/7 support tier
- [ ] Professional services offering

**Milestone:** $200k ARR, 20 Paying Customers, Enterprise Edition GA

---

## 👥 Team Structure & Hiring Plan

### Current Team (May 2026)

| Role | Person | Capacity |
|------|--------|----------|
| Tech Lead / Full-Stack | Developer #1 | 100% |
| Backend Developer | Developer #2 | 100% |
| Frontend Developer | Developer #3 | 100% |

**Total:** 3 people, ~120 hours/week

---

### Hiring Plan

#### Q2 2026
- **No hires** — focus on building with current team

#### Q3 2026
- [ ] **DevOps Engineer** (contractor, part-time)
  - Reason: Need deployment automation & infrastructure
  - Budget: $50/hour, 20 hours/week = $4k/month

#### Q4 2026
- [ ] **Customer Success Manager** (full-time)
  - Reason: Support pilot customers, gather feedback
  - Budget: $80k/year
  
- [ ] **Sales Engineer** (full-time)
  - Reason: Lead demos, close deals
  - Budget: $100k/year + commission

#### Q1 2027
- [ ] **Senior Backend Developer** (full-time)
  - Reason: Scale & performance features
  - Budget: $120k/year

**Total Headcount by Q1 2027:** 7 people

---

## 💰 Budget & Burn Rate

### Development Costs (Q2-Q4 2026)

| Category | Q2 | Q3 | Q4 | Total |
|----------|-----|-----|-----|-------|
| **Salaries** (3 devs @ $100k avg) | $75k | $75k | $75k | $225k |
| **Infrastructure** (hosting, tools) | $2k | $3k | $5k | $10k |
| **Software Licenses** (IDEs, SaaS) | $1k | $1k | $1k | $3k |
| **Marketing** (ads, events) | $2k | $5k | $10k | $17k |
| **Legal** (incorporation, IP) | $5k | $2k | $1k | $8k |
| **Contractors** (DevOps, design) | $5k | $12k | $15k | $32k |
| **Total** | **$90k** | **$98k** | **$107k** | **$295k** |

**Monthly Burn Rate:** ~$33k/month  
**Runway with $500k:** 15 months

---

### Revenue Projections

| Quarter | Pilot Customers | Paying Customers | ARR | Quarterly Revenue |
|---------|-----------------|------------------|-----|-------------------|
| Q2 2026 | 0 | 0 | $0 | $0 |
| Q3 2026 | 5 | 3 | $30k | $7.5k |
| Q4 2026 | 5 | 10 | $100k | $25k |
| Q1 2027 | 5 | 20 | $200k | $50k |
| Q2 2027 | 10 | 35 | $400k | $100k |

**Break-even target:** Q4 2027 (~$400k ARR)

---

## 📈 Success Metrics & KPIs

### Product Metrics

| Metric | Q2 Target | Q3 Target | Q4 Target |
|--------|-----------|-----------|-----------|
| Repositories scanned | 500 | 2,000 | 5,000 |
| Vulnerabilities detected | 1,000 | 5,000 | 15,000 |
| Scan success rate | 95% | 97% | 99% |
| API uptime | 99% | 99.5% | 99.9% |
| Dashboard page load | <2s | <1.5s | <1s |

### Customer Metrics

| Metric | Q2 Target | Q3 Target | Q4 Target |
|--------|-----------|-----------|-----------|
| Community downloads | 100 | 500 | 2,000 |
| Pilot customers | 0 | 5 | 5 |
| Paying customers | 0 | 3 | 10 |
| Customer satisfaction (NPS) | N/A | 50+ | 60+ |
| Churn rate | N/A | 0% | <10% |

### Revenue Metrics

| Metric | Q2 Target | Q3 Target | Q4 Target |
|--------|-----------|-----------|-----------|
| ARR | $0 | $30k | $100k |
| MRR | $0 | $2.5k | $8.3k |
| Average deal size | N/A | $10k | $10k |
| Sales cycle (days) | N/A | 60 | 45 |

---

## 🚨 Risks & Mitigation Strategies

### Technical Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Performance issues at scale** | Medium | High | Load testing, optimize queries, caching |
| **OSV.dev API changes** | Low | High | Vendor diversity (Grype, Trivy fallback) |
| **Security vulnerabilities** | Medium | Critical | Regular security audits, penetration testing |
| **Data migration issues** | Medium | Medium | Comprehensive backup strategy, rollback plan |

### Business Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Low customer adoption** | Medium | Critical | Free Community Edition, aggressive marketing |
| **Competitor enters market** | Medium | High | Focus on differentiation (Azure DevOps native) |
| **Pilot customers churn** | Low | High | Dedicated customer success, rapid issue resolution |
| **Funding shortage** | Low | Critical | Conservative burn rate, early revenue focus |

### Market Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| **Microsoft builds native solution** | Medium | High | Speed to market, build moat with features |
| **Azure DevOps decline** | Low | High | Support GitHub Enterprise Server |
| **Recession impacts budgets** | Medium | Medium | Position as cost-saving vs. cloud solutions |

---

## 🎯 Decision Points & Gates

### Gate 1: End of Q2 2026 (July 31)

**Go/No-Go Criteria:**
- [ ] Community Edition released with 100+ downloads
- [ ] No critical bugs (P0) remaining
- [ ] 2+ pilot customers committed
- [ ] Positive initial feedback (4+ star reviews)

**Decision:** Proceed to v1.0 Production OR Pivot strategy

---

### Gate 2: End of Q3 2026 (October 31)

**Go/No-Go Criteria:**
- [ ] 5 pilot customers live in production
- [ ] At least 1 paying customer (proof of willingness to pay)
- [ ] <5% churn rate
- [ ] Product-market fit validated (NPS 40+)

**Decision:** Launch Professional Edition OR Extend beta phase

---

### Gate 3: End of Q4 2026 (December 31)

**Go/No-Go Criteria:**
- [ ] 10+ paying customers
- [ ] $100k ARR achieved
- [ ] <10% churn rate
- [ ] Scalable sales process established

**Decision:** Raise Series A funding OR Bootstrap to profitability

---

## 📚 Dependencies & Prerequisites

### External Dependencies

| Dependency | Status | Risk |
|------------|--------|------|
| OSV.dev API availability | ✅ Stable | Low |
| Azure DevOps API stability | ✅ Stable | Low |
| .NET 10 support | ✅ GA | Low |
| Angular 19 stability | ✅ Stable | Low |
| Docker Hub availability | ✅ Available | Low |

### Internal Dependencies

| Dependency | Owner | Due Date |
|------------|-------|----------|
| OSV Scanner integration | Dev #1 | May 15 |
| Container scan UI | Dev #3 | May 28 |
| User management | Dev #2 | June 25 |
| Documentation | All | July 9 |
| Beta testing | QA (contractor) | July 23 |

---

## 🏁 Milestones Summary

| Milestone | Target Date | Status | Key Deliverables |
|-----------|-------------|--------|------------------|
| **Alpha Complete** | ✅ April 30, 2026 | Done | Core features functional |
| **Beta Release** | July 31, 2026 | 🟡 In Progress | Community Edition live |
| **v1.0 Production** | August 31, 2026 | 🔴 Not Started | Production-ready platform |
| **3 Pilot Customers** | September 30, 2026 | 🔴 Not Started | Customer validation |
| **First Revenue** | October 31, 2026 | 🔴 Not Started | $10k ARR |
| **Professional Edition** | November 30, 2026 | 🔴 Not Started | Paid tier launched |
| **$100k ARR** | December 31, 2026 | 🔴 Not Started | 10 paying customers |
| **Enterprise Edition** | March 31, 2027 | 🔴 Not Started | Enterprise features |

---

## 📞 Stakeholder Communication

### Weekly Updates (Every Monday)

**Attendees:** Core team (3 devs)  
**Format:** 30-minute standup  
**Topics:**
- Progress vs. plan
- Blockers & risks
- Upcoming priorities

### Monthly Reviews (Last Friday of Month)

**Attendees:** Core team + advisors  
**Format:** 60-minute review  
**Topics:**
- Sprint retrospective
- Metrics review (KPIs)
- Budget vs. actuals
- Customer feedback

### Quarterly Business Reviews (End of Quarter)

**Attendees:** Full stakeholder group  
**Format:** 2-hour strategic session  
**Topics:**
- Quarterly results vs. goals
- Strategy adjustments
- Next quarter planning
- Go/no-go decisions

---

## 📝 Change Log

| Date | Version | Changes | Author |
|------|---------|---------|--------|
| May 1, 2026 | 1.0 | Initial roadmap created | Product Team |
| - | - | - | - |

---

## 🎓 Appendix

### A. Sprint Naming Convention

Format: `YYYY-Q#-S##` (Year-Quarter-Sprint)

Example: `2026-Q2-S01` = Q2 2026, Sprint 1

### B. Priority Definitions

- **P0 (Critical):** Blocking launch, must fix immediately
- **P1 (High):** Important for launch, fix within sprint
- **P2 (Medium):** Nice to have, fix if time permits
- **P3 (Low):** Future enhancement, backlog

### C. Status Indicators

- 🟢 **On Track:** No issues, progressing as planned
- 🟡 **At Risk:** Minor delays, mitigation in place
- 🔴 **Blocked:** Significant issues, needs escalation

---

**Document Owner:** Product Manager  
**Approval Required:** Tech Lead, CEO  
**Next Review:** June 1, 2026 (monthly)  
**Distribution:** All team members, advisors, investors
