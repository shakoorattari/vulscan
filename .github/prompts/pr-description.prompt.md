---
agent: agent
description: Generate a high-quality PR title and description.
---

Instructions
- Title: imperative mood, concise, include scope/component
- Description: include summary, key changes list, rationale, risks, testing notes
- Link related work items (if provided)

Inputs
- scope: ${scope}
- summary: ${summary}
- changes: ${changes}
- risks: ${risks}
- tests: ${tests}
- workItems: ${workItems}

Output template
Title: <generated-title>

## Summary
${summary or generated}

## Key Changes
- ...

## Risks / Mitigations
- ...

## Testing Notes
- ...

## Work Items
- ...
