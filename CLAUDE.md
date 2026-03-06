# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run

```bash
# Pipeline (generates a dated Markdown post)
dotnet build pipeline/
dotnet run --project pipeline/

# Astro site
cd site && npm install
npm run dev          # localhost:4321
npm run build        # static output in site/dist/
```

For local pipeline runs, set at least one AI key plus the Ecowitt keys:
```bash
export OPENAI_API_KEY=...
export ANTHROPIC_API_KEY=...
export ECOWITT_APPLICATION_KEY=...
export ECOWITT_API_KEY=...
export ECOWITT_MAC=...
export POSTS_OUTPUT_DIR=./site/src/content/posts
```
All providers are optional — missing keys are skipped gracefully.

## Architecture

The pipeline (`pipeline/Program.cs`) runs four async data fetches in parallel, merges them into a single AI prompt, queries all configured models, then renders a comparison blog post:

```
EcowittSensorProvider (real-time + 7-day history)
WeatherForecastProvider (Open-Meteo, 7-day forecast)      Program.cs
DaylightProvider (sunrise-sunset.org)                   ──► orchestrates
GardenBedLoader (garden/beds/*.md + optional images)        all steps
        │
        ▼
GardenInsightParser.BuildPrompt()
  replaces {{PLACEHOLDERS}} in garden/prompt.md
        │
        ▼
IInsightGenerator implementations (parallel):
  AnthropicInsightGenerator, OpenAIInsightGenerator
        │
        ▼
GardenInsightParser.ParseInsight()
  splits response on ### section headers
        │
        ▼
BlogPostPublisher.SavePostAsync()
  writes site/src/content/posts/YYYY-MM-DD.md
```

## Key Patterns

**Adding a new data source:** Create a static provider class with an async fetch method. Add the corresponding record(s) to `Models.cs`. Fetch it in parallel in `Program.cs`. Add a `{{PLACEHOLDER}}` to `garden/prompt.md` and wire the replacement in `GardenInsightParser.BuildPrompt()`.

**Adding a new AI model:** Add a new `IInsightGenerator` entry in the `generators` list in `Program.cs`. The interface requires `ProviderName`, `ModelName`, and `GenerateInsightAsync()`. The same prompt goes to every model.

**Garden bed config:** YAML frontmatter + Markdown body in `garden/beds/*.md`. Key frontmatter fields: `name`, `location`, `area_sqm`, `sun`, `soil`, `channels` (maps Ecowitt soil sensors to beds). An image file with the same base name (e.g. `brassica.jpg`) is automatically base64-encoded and sent to vision-capable models.

**Error resilience:** Every provider fetch is try-caught — the pipeline continues without any single data source. Model failures are tracked in `ProviderInsight.Error` and noted in the post. The pipeline only exits non-zero if all models fail.

**Timezone:** All NZ date/time conversions use `GardenConfig.NzTimeZone` (`Pacific/Auckland`). Do not use Windows-style timezone IDs — the pipeline runs on Ubuntu in CI.

**Prompt template:** `garden/prompt.md` contains reasoning rules (growth stage awareness, sensor thresholds, ET0-driven watering) that constrain AI output quality. Changes here directly affect all model outputs.

## CI/CD

GitHub Actions (`.github/workflows/daily-pipeline.yml`) runs at 8:00 AM NZST daily (20:00 UTC). Steps: dotnet run -> astro build -> commit post to repo -> deploy to GitHub Pages. Secrets needed: `ANTHROPIC_API_KEY`, `OPENAI_API_KEY`, `ECOWITT_APPLICATION_KEY`, `ECOWITT_API_KEY`, `ECOWITT_MAC`.

## Coordinates & Location

Hardcoded to Port Chalmers, Dunedin, NZ: latitude -45.8175, longitude 170.6275. These appear in `WeatherForecastProvider.cs` and `DaylightProvider.cs`.
