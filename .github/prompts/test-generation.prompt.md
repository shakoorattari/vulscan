---
agent: agent
description: Generate unit/integration tests following project rules.
---

Follow:
- .github/instructions/test-generation.instructions.md
- .github/instructions/copilot.instructions.md

Inputs (optional)
- language: ${language:C#|TypeScript}
- target: ${targetPath}

Steps
1) Identify public surface to test (controllers/services/repos/components).
2) Create tests per guidelines: Arrange-Act-Assert, mock dependencies, parameterized cases where helpful.
3) Cover happy path + 1–2 edge cases; assert error handling.
4) Keep explicit types, avoid any; no logic inside tests.
5) Name files and classes per conventions.

Output
- Test file(s) with concise comments for complex logic
- Short run instructions if non-standard
