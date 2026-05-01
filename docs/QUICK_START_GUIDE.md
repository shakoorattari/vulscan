# 🚀 Vulscan — Quick Start Guide for Stakeholders

**Last Updated:** May 1, 2026  
**Status:** Beta Development  
**Next Milestone:** Community Edition Release (July 31, 2026)

---

## 📋 TL;DR — What is Vulscan?

**Vulscan** is an enterprise vulnerability management platform for **on-premises Azure DevOps Server** environments.

### Elevator Pitch (30 seconds)

> "Vulscan is like **Snyk for Azure DevOps on-premises**. We help enterprises automatically scan their repositories for vulnerabilities, track them over time, and generate compliance reports — all while keeping their data behind the firewall. Powered by Google's OSV.dev database, priced at 1/5th of Snyk's cost."

---

## 🎯 Key Documents (Read These First)

| Document | Purpose | Audience | Time to Read |
|----------|---------|----------|--------------|
| [README.md](../README.md) | Technical overview | Developers | 5 min |
| [STRATEGIC_POSITIONING.md](./STRATEGIC_POSITIONING.md) | Why we exist, market position | Leadership, investors | 15 min |
| [PROJECT_PLAN_2026.md](./PROJECT_PLAN_2026.md) | Roadmap, timeline, budget | Team, stakeholders | 20 min |
| [OSV_COMPARISON_AND_RECOMMENDATIONS.md](./OSV_COMPARISON_AND_RECOMMENDATIONS.md) | Technical strategy | Engineers | 15 min |

---

## 🏆 Why Vulscan Matters

### The Problem We Solve

**Enterprise customers with on-premises Azure DevOps Server have NO good solution for:**
1. ❌ Automated vulnerability scanning across all repositories
2. ❌ Centralized dashboard for security teams
3. ❌ Historical tracking for compliance (SOC 2, ISO 27001)
4. ❌ On-premises deployment (cloud solutions rejected for data sovereignty)

### Our Solution

✅ **Native Azure DevOps integration** (PAT, collections, projects)  
✅ **Web dashboard** (Angular SPA for non-technical stakeholders)  
✅ **Historical analysis** (track trends over months/years)  
✅ **On-premises first** (data never leaves customer network)  
✅ **Powered by OSV.dev** (Google's free, comprehensive vulnerability database)  
✅ **Affordable** ($10k-50k/year vs. $100k+ for Snyk Enterprise)

---

## 💰 Business Model Snapshot

### Target Customers

1. **Enterprise Security Teams** (1000+ employees, 500+ repos)
2. **Government Agencies** (air-gapped networks)
3. **Financial Institutions** (data sovereignty requirements)
4. **Healthcare Organizations** (HIPAA compliance)

### Revenue Model

| Edition | Price | Target |
|---------|-------|--------|
| **Community** | **Free** | Individual teams, open-source projects |
| **Professional** | **$10k/year** | Mid-sized companies (100-500 repos) |
| **Enterprise** | **$50k/year** | Large enterprises, government |
| **Managed Service** | **$100k+/year** | Global enterprises (custom deployment) |

### Revenue Goals

- **Q3 2026:** $30k ARR (3 customers)
- **Q4 2026:** $100k ARR (10 customers)
- **Q1 2027:** $200k ARR (20 customers)
- **Break-even:** Q4 2027 (~$400k ARR)

---

## 🗓️ Timeline at a Glance

### Q2 2026 (Now — July) — **Beta Release**

**Focus:** Complete core features, launch Community Edition

**Key Milestones:**
- ✅ Add container scanning (OSV Scanner CLI)
- ✅ Add license compliance
- ✅ Polish dashboard UI
- ✅ User management & RBAC
- ✅ Documentation & installers
- 🎉 **Launch Community Edition (July 31)**

**Team:** 3 developers  
**Budget:** $90k

---

### Q3 2026 (Aug-Oct) — **v1.0 Production**

**Focus:** Production hardening, pilot customers

**Key Milestones:**
- ✅ SQL Server support (enterprise DB)
- ✅ High availability deployment
- ✅ Onboard 5 pilot customers
- ✅ First paying customer ($10k)
- 🎉 **$30k ARR (October 31)**

**Team:** 3 devs + 1 contractor (DevOps)  
**Budget:** $98k

---

### Q4 2026 (Nov-Dec) — **Professional Edition**

**Focus:** Commercialization, growth

**Key Milestones:**
- ✅ Multi-instance management
- ✅ SSO/SAML authentication
- ✅ Licensing system
- ✅ Sales & marketing launch
- 🎉 **$100k ARR, 10 customers (December 31)**

**Team:** 5 people (2 new hires)  
**Budget:** $107k

---

### Q1 2027 (Jan-Mar) — **Enterprise Edition**

**Focus:** Enterprise features, scale

**Key Milestones:**
- ✅ Air-gapped deployment
- ✅ Advanced RBAC
- ✅ 24/7 support tier
- 🎉 **$200k ARR, 20 customers (March 31)**

**Team:** 7 people  
**Budget:** $120k

---

## 📊 Success Metrics

### Product Health

| Metric | Current | Q2 Target | Q3 Target | Q4 Target |
|--------|---------|-----------|-----------|-----------|
| Repositories scanned | 100 | 500 | 2,000 | 5,000 |
| Vulnerabilities detected | 200 | 1,000 | 5,000 | 15,000 |
| API uptime | 99% | 99% | 99.5% | 99.9% |
| Dashboard load time | 2.5s | <2s | <1.5s | <1s |

### Business Health

| Metric | Current | Q2 Target | Q3 Target | Q4 Target |
|--------|---------|-----------|-----------|-----------|
| Community downloads | 0 | 100 | 500 | 2,000 |
| Pilot customers | 0 | 0 | 5 | 5 |
| Paying customers | 0 | 0 | 3 | 10 |
| ARR | $0 | $0 | $30k | $100k |
| Customer satisfaction | N/A | N/A | 50+ NPS | 60+ NPS |

---

## 🎯 Current Sprint (May 1-14, 2026)

### Sprint 1: Container & License Scanning

**Goal:** Integrate OSV Scanner CLI for advanced features

**Tasks:**
- [ ] Install OSV Scanner on development machines
- [ ] Create `OsvScannerService.cs` wrapper service
- [ ] Add `ContainerScansController.cs` REST API
- [ ] Add `LicenseScansController.cs` REST API
- [ ] Database migrations (new tables)
- [ ] Unit tests
- [ ] Integration tests

**Blockers:** None  
**Status:** 🟢 On track  
**Confidence:** 90% we'll finish on time

---

## 👥 Team & Roles

### Current Team (May 2026)

| Name | Role | Responsibilities |
|------|------|------------------|
| Developer #1 | Tech Lead | Architecture, backend, DevOps |
| Developer #2 | Backend Dev | API, database, integrations |
| Developer #3 | Frontend Dev | Angular dashboard, UI/UX |

### Hiring Plan

| Role | Start Date | Why |
|------|------------|-----|
| DevOps Engineer (contractor) | July 2026 | Deployment automation |
| Customer Success Manager | October 2026 | Support pilot customers |
| Sales Engineer | November 2026 | Demos, deal closing |
| Senior Backend Developer | January 2027 | Scale & performance |

---

## 💡 Competitive Positioning

### We Win Against Cloud Solutions (Snyk, GitHub)

**Why customers choose us:**
1. ✅ **On-premises deployment** (data sovereignty)
2. ✅ **Azure DevOps native** (not GitHub-first)
3. ✅ **5x cheaper** ($10k vs. $50k-100k)
4. ✅ **Air-gapped support** (government, military)

**Target:** Enterprises that can't/won't use cloud SaaS

---

### We Win Against CLI Tools (OSV Scanner, Grype)

**Why customers choose us:**
1. ✅ **Web dashboard** (security teams don't run CLI)
2. ✅ **Automation** (scheduled scans, no manual work)
3. ✅ **Historical tracking** (compliance requirements)
4. ✅ **Multi-repo management** (centralized visibility)
5. ✅ **Notifications** (proactive email/Teams alerts)

**Target:** Enterprises that need centralized management

---

### We Use OSV.dev (Not Compete)

**Strategic Rationale:**
- ✅ Google maintains 100k+ vulnerabilities (we don't have to)
- ✅ Free API, no rate limits (zero infrastructure cost)
- ✅ "Powered by Google OSV.dev" = credibility
- ✅ Focus on our differentiation (Azure DevOps, UI, workflows)

**We build the enterprise platform on top of open-source infrastructure** ✨

---

## 🚨 Risks & Mitigation

### Top 3 Risks

| Risk | Impact | Mitigation |
|------|--------|------------|
| **Low customer adoption** | 🔴 Critical | Free Community Edition, aggressive marketing |
| **Microsoft builds native solution** | 🔴 High | Speed to market, build feature moat |
| **Performance issues at scale** | 🟡 Medium | Load testing, optimization sprints |

---

## 📞 Who to Contact

### Internal Team

- **Tech Lead:** Developer #1 (architecture, technical decisions)
- **Product Manager:** [TBD] (roadmap, customer needs)
- **Project Manager:** Developer #1 (interim, until PM hired)

### External Support

- **OSV.dev Community:** [GitHub Discussions](https://github.com/google/osv.dev/discussions)
- **Azure DevOps API:** [Microsoft Docs](https://docs.microsoft.com/en-us/rest/api/azure/devops/)

---

## 🎓 Learning Resources

### For New Team Members

**Week 1: Understand the Problem**
- [ ] Read [STRATEGIC_POSITIONING.md](./STRATEGIC_POSITIONING.md)
- [ ] Watch [Azure DevOps Overview](https://www.youtube.com/watch?v=JhqpF-5E10I)
- [ ] Review competitor products (Snyk, Trivy, OSV Scanner)

**Week 2: Understand Our Solution**
- [ ] Read [README.md](../README.md) and [BRD v3](./brd-v03.md)
- [ ] Set up local development environment
- [ ] Run the application, explore the dashboard
- [ ] Review [CVE_INTEGRATION_GUIDE.md](./CVE_INTEGRATION_GUIDE.md)

**Week 3: Start Contributing**
- [ ] Pick a P2 task from current sprint
- [ ] Submit first PR
- [ ] Attend team standup & sprint planning

---

## 📈 Next Actions (This Week)

### For Developers
1. ⏰ **Today:** Review [OSV_SCANNER_INTEGRATION_IMPLEMENTATION.md](./OSV_SCANNER_INTEGRATION_IMPLEMENTATION.md)
2. ⏰ **Tomorrow:** Install OSV Scanner CLI locally
3. ⏰ **By Friday:** Complete `OsvScannerService.cs` implementation

### For Leadership
1. 📊 **This Week:** Review [PROJECT_PLAN_2026.md](./PROJECT_PLAN_2026.md)
2. 🤝 **This Week:** Identify 3 potential pilot customers
3. 💰 **This Month:** Finalize Q2 budget

### For Sales/Marketing
1. 📝 **This Week:** Draft first blog post ("Why Vulscan?")
2. 🎥 **This Month:** Record demo video (3-5 minutes)
3. 🌐 **Next Month:** Set up website landing page

---

## ✅ Definition of Done (v1.0 Launch Checklist)

### Product
- [ ] All core features complete (scanning, dashboard, reports)
- [ ] Container & license scanning working
- [ ] No P0/P1 bugs remaining
- [ ] Performance tested (1000+ repos)
- [ ] Security audit passed

### Go-to-Market
- [ ] Documentation complete (user guide, admin guide, API docs)
- [ ] Installers working (Windows, Linux)
- [ ] Video tutorials published
- [ ] Pricing page live
- [ ] Sales collateral ready

### Customer Success
- [ ] 5 pilot customers onboarded
- [ ] Support system operational
- [ ] Customer feedback loop established
- [ ] Case studies drafted

---

## 🎉 Celebrate Milestones

### Upcoming Celebrations

| Milestone | Date | How We'll Celebrate |
|-----------|------|---------------------|
| Sprint 1 Complete | May 14 | Team lunch 🍕 |
| Community Edition Launch | July 31 | Team dinner + Product Hunt launch 🚀 |
| First Paying Customer | October 2026 | Champagne toast 🥂 |
| $100k ARR | December 2026 | Team offsite retreat 🏖️ |

---

## 📚 Quick Links

### Documentation
- [Project Plan](./PROJECT_PLAN_2026.md)
- [Strategic Positioning](./STRATEGIC_POSITIONING.md)
- [OSV Comparison](./OSV_COMPARISON_AND_RECOMMENDATIONS.md)
- [BRD v3](./brd-v03.md)

### Technical
- [OSV.dev API](https://google.github.io/osv.dev/api/)
- [OSV Scanner Docs](https://google.github.io/osv-scanner/)
- [Azure DevOps API](https://docs.microsoft.com/en-us/rest/api/azure/devops/)

### Community
- [GitHub Repo](https://github.com/shakoorattari/vulscan)
- [Issue Tracker](https://github.com/shakoorattari/vulscan/issues)

---

**Questions? Reach out to the team lead!** 💬

**Last Updated:** May 1, 2026  
**Version:** 1.0  
**Next Update:** June 1, 2026
