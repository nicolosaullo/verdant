# 🌱 Verdant

> AI-powered daily garden insights from live sensor data — compared across Claude and GPT models, published as a static blog.

A .NET 8 pipeline reads soil moisture, temperature, and humidity from an Ecowitt sensor
network, queries **multiple AI models in parallel**, and commits a Markdown comparison post
to the repo. GitHub Actions then builds the Astro site and deploys it to GitHub Pages — so
every morning a fresh post appears with no manual work.

## How It Works

```
Ecowitt sensors
      │
      ▼
.NET 8 pipeline ──► Anthropic API  (claude-opus-4-6, claude-sonnet-4-6)
      │         └──► OpenAI API    (gpt-4o, gpt-4o-mini)
      │                    │ (parallel)
      ▼                    ▼
 Markdown comparison post  ←────────────────┘
      │
      ▼
 git commit/push ──► GitHub Actions build ──► GitHub Pages
```

**Sensors (Ecowitt)**
- GW1200 gateway — local Wi-Fi hub that polls all sensors every 5 minutes
- WH51 soil moisture sensors — one per bed
- WN32 outdoor temperature/humidity

**Pipeline (`pipeline/`)**
| File | Purpose |
|------|---------|
| `Models.cs` | All shared records + `IInsightGenerator` interface |
| `GardenInsightParser.cs` | Shared prompt builder and response parser |
| `AnthropicInsightGenerator.cs` | Calls the Anthropic API |
| `OpenAIInsightGenerator.cs` | Calls the OpenAI API |
| `MockSensorProvider.cs` | Simulates Ecowitt data; swap for real client when ready |
| `BlogPostPublisher.cs` | Renders a per-model comparison post as Markdown |
| `Program.cs` | Orchestrates the full pipeline |

**Site (`site/`)**
- Astro v4 static site with a content collection for posts
- Minimal, readable layout with sensor data tables and per-model sections
- Deployed to GitHub Pages via the Actions workflow

## Repo Structure

```
verdant/
├── pipeline/
│   ├── GardenAI.csproj
│   ├── Program.cs
│   ├── Models.cs
│   ├── GardenInsightParser.cs
│   ├── AnthropicInsightGenerator.cs
│   ├── OpenAIInsightGenerator.cs
│   ├── MockSensorProvider.cs          ← swap for real EcowittClient here
│   └── BlogPostPublisher.cs
├── site/
│   ├── package.json
│   ├── astro.config.mjs               ← set your GitHub Pages URL here
│   ├── tsconfig.json
│   └── src/
│       ├── content/
│       │   ├── config.ts              ← Astro content collection schema
│       │   └── posts/                 ← generated .md files land here
│       ├── layouts/
│       │   ├── Base.astro
│       │   └── PostLayout.astro
│       ├── pages/
│       │   ├── index.astro
│       │   └── posts/[...slug].astro
│       └── styles/
│           └── global.css
├── .github/
│   └── workflows/
│       └── daily-pipeline.yml
├── .gitignore
└── README.md
```

## Setup

### 1. GitHub Repository Secrets

Go to **Settings → Secrets and variables → Actions** and add:

| Secret | Value |
|--------|-------|
| `ANTHROPIC_API_KEY` | Your key from console.anthropic.com (`sk-ant-...`) |
| `OPENAI_API_KEY` | Your key from platform.openai.com (`sk-...`) |

`GITHUB_TOKEN` (for the bot commit and Pages deploy) is provided automatically.

### 2. Enable GitHub Pages

1. Go to **Settings → Pages**
2. Set **Source** to **GitHub Actions** (not "Deploy from a branch")
3. That's it — the workflow handles the rest

### 3. Update `site/astro.config.mjs`

Edit the two lines at the top of the config to match your repository:

```js
// For a project repo at https://username.github.io/verdant/
site: 'https://username.github.io',
base: '/verdant',

// For a user/org site at https://username.github.io/
site: 'https://username.github.io',
// (remove the base line)

// For a custom domain at https://garden.example.com/
site: 'https://garden.example.com',
// (remove the base line, and add a CNAME file to site/public/)
```

Commit this change before the first pipeline run.

### 4. Local Development

**Run the pipeline (writes a post into the site's content directory):**
```bash
export ANTHROPIC_API_KEY=sk-ant-...
export OPENAI_API_KEY=sk-...
export POSTS_OUTPUT_DIR=./site/src/content/posts
dotnet run --project pipeline/
```

**Preview the Astro site:**
```bash
cd site
npm install
npm run dev        # http://localhost:4321
```

**Build the static site:**
```bash
cd site && npm run build   # output in site/dist/
```

### 5. Adding or Removing Models

Edit the `generators` array in `pipeline/Program.cs`:

```csharp
IInsightGenerator[] generators =
[
    new AnthropicInsightGenerator(anthropicApiKey, "claude-opus-4-6"),
    new AnthropicInsightGenerator(anthropicApiKey, "claude-sonnet-4-6"),
    new OpenAIInsightGenerator(openAiApiKey, "gpt-4o"),
    new OpenAIInsightGenerator(openAiApiKey, "gpt-4o-mini"),
];
```

All models are queried in parallel; the post includes one labelled section per model.
If a model fails its API call, it is noted in the post but doesn't block publication.

## Switching to Real Ecowitt Data

`pipeline/MockSensorProvider.cs` simulates Ecowitt readings. When you're ready for live
data, replace its two methods with calls to your GW1200's local API:

1. Find your gateway's local IP (Ecowitt app → Settings → Network)
2. Call `GET http://<gateway-ip>/api/v1/device/real_time?application_key=...&api_key=...`
3. Map the response JSON onto `SensorReading` and `DailyHistory` in `Models.cs`

Everything downstream (AI generation, post rendering, git commit) is unchanged.

## Schedule

The pipeline runs at **8:00 am NZST** every day (20:00 UTC).

Manual run: **Actions → Daily Garden AI Pipeline → Run workflow**.

## Tech Stack

| Layer | Technology |
|-------|------------|
| Sensors | Ecowitt GW1200, WH51, WN32 |
| Pipeline | .NET 8, C# |
| AI — Anthropic | claude-opus-4-6, claude-sonnet-4-6 |
| AI — OpenAI | gpt-4o, gpt-4o-mini |
| Blog | Astro v4 |
| Hosting | GitHub Pages |
| CI/CD | GitHub Actions |
