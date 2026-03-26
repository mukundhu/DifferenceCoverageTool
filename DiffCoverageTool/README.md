# DiffCoverageTool

A highly flexible .NET CLI tool and robust Web Dashboard that determines unit test code coverage specifically for **newly added or modified code** since a given commit, or dynamically generates a master **Full Project Coverage** report for your entire repository natively bypassing Git.

This tool supports both **.NET `dotnet test`** and **Angular `npm run test`** architectures organically out-of-the-box, easily navigating large multi-project repositories securely.

## Core Features 🚀
- **Multi-Framework Auto-Detect**: Effortlessly toggles between mapping `.NET Core` test coverage and `Angular Karma/Jest` workspaces.
- **Microservices & Monorepos**: Recursively discovers multiple test projects natively across your entire codebase and logically aggregates their reports in one unified dashboard, separated automatically by `<package name="...">` service definitions.
- **Full Project Coverage**: Flip a single switch in the Dashboard to map Coverage locally without ever querying your git differentials. 
- **Uncommitted Changes Tracker**: Check your live modified uncommitted file deltas straight out of the UI organically.
- **Export to PDF Data Tables**: Cleanly generates professional `.pdf` Data Datatables of your Coverable, Covered, and Output percentage statistics seamlessly grouped natively by Service names.
- **Fail-Fast Error Dashboards**: Automatically traps, intercepts, and prints raw terminal exceptions (`MSBuild` failures or `karma` syntax traps) right at the top of the HTML console natively if the test engines exit with a non-zero exit code!

## Prerequisites
- .NET 8.0 SDK (or whichever version you compiled the tool with)
- Node.js (v14+ recommended for the Web Dashboard)
- Git installed and accessible from the command line

## How to Run

### Option 1: Interactive Web Dashboard (Easiest Method)

We've added a stunning, interactive Node.js Web Dashboard. It allows you to select your repository path, pick your target commits (including uncommitted files), specify your Framework (`.NET` or `Angular`), and instantly visually inspect the detailed metrics table.

To launch the dashboard natively from the root of this exact repository, effortlessly execute the platform-specific scripts provided:

**Windows:**
Double-click `start.bat` or run:
```cmd
.\start.bat
```

**Mac/Linux:**
```bash
sh start.sh
```
*(These scripts automatically install Node.js dependencies and launch the server implicitly via port `3000`. The C# project builds automatically when natively requested by the dashboard backend.)*

### Option 2: Using `dotnet run` (CLI/CI Pipeline Usage)

You can run the engine heavily directly from its standalone core code directory to integrate natively into pipelines. Open a terminal securely inside the `DiffCoverageTool` folder, then seamlessly orchestrate with `dotnet run`:

```bash
# Syntax
dotnet run --project <path-to-DiffCoverageTool.csproj> "<path-to-target-repo>" <base-git-ref> <framework-type>

# Example: Run Full Coverage analysis dynamically against an Angular web project
dotnet run --project DiffCoverageTool.csproj "C:\path\to\your\angular-project" FULL_COVERAGE angular

# Example: Check uncommitted changes specifically against HEAD inside a NET monolith
dotnet run --project DiffCoverageTool.csproj "C:\path\to\your\csharp-solution" HEAD dotnet
```

## Troubleshooting

- **"No modified files found"**: This means your active `git diff` tracker didn't return any codebase deltas! Ensure you have made changes logically relative to the reference node, or select `Full Project Coverage` to map the entire repository organically instead!
- **"Could not find coverage.cobertura.xml"**: The engine successfully executed the testing runners, but the respective coverage logfile was missing. If using .NET, make sure the project actively references `coverlet.collector`! If using Angular, securely append `--code-coverage=true` in your root environment architectures!
