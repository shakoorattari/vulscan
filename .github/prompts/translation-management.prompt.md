---
agent: agent
description: Scan code for translation keys, compare with TransLynk, generate and insert missing translations.
tools: ['search', 'filesystem/*', 'sequential-thinking/*', 'translynk/*', 'todos']
---

# Translation Management Workflow

## Supported
- Languages: English (en), Arabic (ar)
- File Types: .cs, .html, .ts, .json

## Workflow Steps
1. Scan selected directories recursively for translatable strings (UI text, messages, validation, titles, placeholders).
2. Extract keys from below using similar patterns:
   - TypeScript: `this.translateService.instant('Pin')` → Pin
   - TypeScript: `this.toast.showSuccess('App added to quick access successfully.', 'Success');` → App added to quick access successfully & Success etc.
   - HTML: `[labelKey]="'email type'"`, `[titleKey]="'enter email type'"`, `[placeholderKey]="'please enter email type'"`
   - Angular templates: `{{ 'email type' | translate }}`
   - C#: `_biTranslation.SetEResponseMessagesAsync(_eResponse, "some error occurred", lang)`
   - C# DataAnnotations: `[Required(ErrorMessage = "Timestamp is required.")]`
   - Hard-coded user-facing strings in C# and TS
3. Normalize keys (trim whitespace; preserve case for comparison; skip config/technical values).
4. Retrieve all existing translations from TransLynk (keys only).
5. Compare (case-sensitive by key only):
   - ✅ Existing (exact key match)
   - ❌ Missing (key not present)
   - 🔄 Needs update (present, but content may be reviewed later)
6. For ❌ Missing keys, generate clear English and UAE-appropriate Arabic.
7. Insert new keys in batches (50–100). Only insert if the key does not exist.
8. Iterate until all keys are processed; handle retries on failures.
9. Report summary: files scanned, keys found, existing/missing/inserted/failed, plus improvement tips.

## Quality & Technical Standards
- Professional, consistent language (en/ar)
- Project key naming conventions
- No duplicate keys (compare by key only, case-sensitive)
- Valid JSON structures for inserts
- Validate translations before insert

## Usage Examples
- Scan a folder: Scan modules/admin-users for translation keys
- Scan files: Scan selected files for missing translations
- Full audit: Audit src/angular-app/src/app for translations

## Notes
- Always check for existing keys before insert (by key only, case-sensitive)
- Do not skip insertion due to value matches on other keys
- Prefer batch processing for inserts
- Test translations in-app after update
- Use emojis (✅❌🔄) in reports for clarity

100% score = all keys found, translated, and inserted with no errors.

