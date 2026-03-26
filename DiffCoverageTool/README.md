# DiffCoverageTool

A highly flexible .NET CLI tool and interactive Web Dashboard for code coverage analysis. Supports **diff coverage** (new/modified lines only), **full project coverage**, and **selective per-service testing** across .NET and Angular monorepos.

## Core Features 🚀

| Feature | Description |
|---|---|
| **Multi-Framework** | Supports `.NET Core` (`dotnet test`) and `Angular` (`npm run test`) out of the box |
| **Monorepo / Microservices** | Recursively discovers all projects in a folder — no root `.sln` required |
| **Select Services** | Checkbox list auto-populates on folder load; run coverage for only the services you choose |
| **Diff or Full Coverage** | Toggle between covering only changed lines vs. entire codebase |
| **Report Mode** | Choose: `File Detail Only`, `File Detail + Service Summary`, or `Summary Only` |
| **Export to PDF** | One-click export of the Service Summary table to a clean printable PDF |
| **Fail-Fast Errors** | Test runner failures are surfaced directly inside the HTML report |
| **Clear Reports** | Button to delete all generated `TestResults/` folders and coverage XML/HTML files |
| **Uncommitted Changes** | Compare against live working-tree changes without committing |

## Prerequisites
- .NET 8.0 SDK
- Node.js v14+
- Git (for diff mode)

## How to Run

### Option 1: Web Dashboard (Recommended)

**Windows:**
```cmd
.\start.bat
```

**Mac/Linux:**
```bash
sh start.sh
```

Open **http://localhost:3000** in your browser.

#### Dashboard Workflow
1. Enter the root folder of your repository (can contain multiple services).
2. Select **Framework** (`.NET Core` or `Angular`) and click **Load Commits**.
3. A **Services** checklist appears — check only the projects you want to test.
4. Choose a **Coverage Mode** (`Diff Coverage` or `Full Project`).
5. In `Diff Coverage` mode, pick your **Base** and **Target** commits.
6. Select a **Report Mode** from the dropdown.
7. Click **Run Test Cases**.

### Option 2: CLI / CI Pipeline

```bash
# Syntax
dotnet run --project DiffCoverageTool.csproj "<repoPath>" <diffArg> <framework> <selectedProjects> <reportMode>

# Full coverage — all services — .NET
dotnet run --project DiffCoverageTool.csproj "C:\repos\MyApp" FULL_COVERAGE dotnet ALL detail-and-summary

# Diff coverage — specific services — .NET (pipe-separate paths)
dotnet run --project DiffCoverageTool.csproj "C:\repos\MyApp" HEAD~1 dotnet "C:\repos\MyApp\ServiceA|C:\repos\MyApp\ServiceB" detail-only

# Angular full coverage
dotnet run --project DiffCoverageTool.csproj "C:\repos\my-ng-app" FULL_COVERAGE angular ALL summary-only
```

**Argument reference:**

| Position | Value | Default |
|---|---|---|
| 1 | Repository path | current directory |
| 2 | Git ref or `FULL_COVERAGE` | `HEAD~1` |
| 3 | `dotnet` or `angular` | `dotnet` |
| 4 | Pipe-separated project paths or `ALL` | `ALL` |
| 5 | `detail-only` / `detail-and-summary` / `summary-only` | `detail-only` |

## Troubleshooting

- **"No modified files found"**: No git diff was detected. Use `Full Project` mode or change your base commit.
- **"Could not find coverage.cobertura.xml"**: Ensure your test project references `coverlet.collector` (.NET) or that `--code-coverage` is enabled in `karma.conf.js` (Angular).
- **"No matching projects found"**: The selected service paths don't match any discovered `.csproj` directories. Try clicking **Load Commits** again to refresh the services list.

