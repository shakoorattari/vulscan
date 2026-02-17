---
agent: agent
description: Perform a comprehensive code review for a PR or selected changes using project standards.
tools: ['search', 'vscode/getProjectSetupInfo', 'vscode/installExtension', 'vscode/newWorkspace', 'vscode/runCommand', 'execute/getTerminalOutput', 'execute/runInTerminal', 'read/terminalLastCommand', 'read/terminalSelection', 'execute/createAndRunTask', 'execute/runTask', 'read/getTaskOutput', 'context7/*', 'filesystem/*', 'sequential-thinking/*', 'search/usages', 'vscode/vscodeAPI', 'read/problems', 'search/changes', 'execute/testFailure', 'vscode/openSimpleBrowser', 'web/fetch', 'vscode/extensions', 'todo', 'execute/runTests','azure-devops/*']
---

#mcp_azure_devops__analyze_pull_request_code_quality
```json
{
  "repositoryId": "Vulscan",
  "pullRequestId": ${pullRequestId},
  "checks": [
    "all"
  ],
  "autoComment": false,
  "codeReviewInstructions": {
    "file": "${workspaceFolder}/.github/instructions/code-review.instructions.md"
  },
}
```


Provide a thorough code review that strictly follows our guidance in:

- .github/instructions/code-review.instructions.md
- .github/instructions/copilot.instructions.md
- .github/instructions/.copilot-codeGeneration.instructions.md

Inputs (supply when running):
- repositoryId: ${repositoryId}
- pullRequestId: ${pullRequestId}
- targetBranch: ${targetBranch:dev}
- if no PR context is provided, review the currently selected files in the editor.

Review workflow:
1) Context gathering
  - If pullRequestId is provided, fetch PR details and the file changes; otherwise, gather the list of selected or staged files.
  - Identify newly created vs modified files.

2) High-level checks
  - Verify merge target is `${targetBranch}` (default dev).
  - Validate scope: small, incremental, and focused.

3) File-by-file analysis
  For each new/changed file, check:
  - Placement and naming (per architecture and conventions).
  - Separation of concerns, DI usage, service registration (if applicable).
  - Backend: layering (Controllers/Services/Repositories/DTOs), caching via ICacheProvider, async usage, AsNoTracking for read queries, error handling/logging.
  - Frontend: Tailwind usage, SDD components, explicit TS types (avoid any), reactive form validation, permission directives, localization.
  - Security: auth/authorization attributes, input validation, parameterized queries, HTTPS/CORS.
  - Tests/docs: presence or clear plan to add; Swagger/XML comments where applicable.

4) Security Verification
  - Check the instructions/prompt in file "${workspaceFolder}/.github/prompts/security-review.prompt.md"

5) Gaps and actions
  - List concrete remediation items per file (actionable, minimal changes).
  - Call out risky or ambiguous changes and request clarifications.

6) Ratings
  - Per-file score (0-100%) and severity signals (✅ minor, ⚠️ moderate, ❌ major).
  - Overall PR score and merge readiness (Block/Needs Changes/Approve).

Output format:
- Summary
- New files table (path, purpose, placement OK?)
- Modified files table (path, change summary)
- Findings per file (bullets with code references if possible)
- Remediation checklist
- Ratings (per file + overall)

Notes:
- Prefer small, pointed suggestions with code examples where useful.
- Reference exact files/methods to aid navigation.
 (See <attachments> above for file contents. You may not need to search or read the file again.)
