---
name: verdant-issue-workflow
description: Full Verdant pipeline workflow — check GitHub issues, implement changes, run locally, commit/push, trigger GHA, verify. Use when the user asks to work on open issues or says "check the issues".
disable-model-invocation: false
allowed-tools: Bash, Read, Edit, Write, Glob, Grep
argument-hint: [issue-number(s) or empty for all open]
---

You are working on the **Verdant garden AI pipeline** at `C:\dev\nic\verdant`.

Follow these steps in order. Create a task list with TaskCreate at the start to track progress.

## 1 — Check GitHub Issues

```bash
gh issue list --repo nicolosaullo/verdant
```

If `$ARGUMENTS` is given, fetch only those issue numbers:
```bash
gh issue view $ARGUMENTS --repo nicolosaullo/verdant
```

Otherwise fetch **all open issues** and read each one fully:
```bash
gh issue view <N> --repo nicolosaullo/verdant
```

Understand every issue before writing any code.

## 2 — Read Relevant Files

Before touching any file, read it first. Key files:

- `pipeline/WeatherForecastProvider.cs` — Open-Meteo fetch + ForecastDay record
- `pipeline/DaylightProvider.cs` — sunrise-sunset.org fetch
- `pipeline/Models.cs` — shared records + IInsightGenerator interface
- `pipeline/GardenInsightParser.cs` — prompt builder + parser
- `pipeline/BlogPostPublisher.cs` — Markdown post renderer
- `pipeline/Program.cs` — orchestration / parallel fetches
- `pipeline/AnthropicInsightGenerator.cs` — Anthropic API call
- `pipeline/OpenAIInsightGenerator.cs` — OpenAI API call

## 3 — Implement Changes

Edit only the files that need changing. Keep changes minimal and focused.

Rules:
- New providers → create `pipeline/<Name>Provider.cs`, add record to `Models.cs`, fetch in parallel in `Program.cs`
- New prompt context → add `{{PLACEHOLDER}}` replacement in `GardenInsightParser.BuildPrompt()`
- New forecast columns → extend `ForecastDay` record, URL query string, `ToPromptTable()`, and `BlogPostPublisher` table
- Always thread new data through `IInsightGenerator.GenerateInsightAsync()` signature if it affects the prompt

## 4 — Build and Verify

```bash
cd /c/dev/nic/verdant && dotnet build pipeline/ 2>&1
```

Fix any compile errors before proceeding.

## 5 — Run the Pipeline Locally

```bash
cd /c/dev/nic/verdant && dotnet run --project pipeline/ 2>&1
```

Inspect the generated post in `output/posts/` to confirm new data is present.

## 6 — Commit and Push

Stage only pipeline files (not generated output):
```bash
git add pipeline/
git commit -m "..."
git push origin main
```

Reference the closed issue numbers in the commit message (`closes #N`).

## 7 — Trigger the GHA Workflow

```bash
gh workflow run daily-pipeline.yml --repo nicolosaullo/verdant
```

Then watch it to completion:
```bash
gh run watch <run-id> --repo nicolosaullo/verdant
```

Or poll:
```bash
gh run list --repo nicolosaullo/verdant --limit 1
```

## 8 — Verify Success

Confirm every step in the workflow log shows ✓. Report the final result to the user.
