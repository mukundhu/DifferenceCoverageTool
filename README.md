# DiffCoverageTool

A .NET CLI tool that determines unit test code coverage specifically for **newly added or modified code** since a given commit. This helps ensure that new changes are adequately tested without being skewed by the overall project coverage percentage.

## Prerequisites
- .NET 8.0 SDK (or whichever version you compiled the tool with)
- Git installed and accessible from the command line

## How It Works
1. Runs `git diff -U0 <base-ref>` to find all added or modified lines in the target repository.
2. Runs `dotnet test --collect:"XPlat Code Coverage"` on the target repository to generate a Cobertura XML report.
3. Intersects the modified lines from Git with the coverage metrics from Cobertura to calculate exactly how much of your new code is covered by tests.

## How to Run

### Option 1: Using `dotnet run` (Recommended for testing)

You can run the tool directly from its source code directory. Open a terminal and navigate to the `DiffCoverageTool` folder, then use `dotnet run`:

```bash
# Syntax
dotnet run --project <path-to-DiffCoverageTool.csproj> "<path-to-target-repo>" <base-git-ref>

# Example: Run against your working directory, comparing against your last commit (HEAD~1)
dotnet run --project DiffCoverageTool.csproj "C:\path\to\your\project" HEAD~1

# Example: To check uncommitted changes only against HEAD
dotnet run --project DiffCoverageTool.csproj "C:\path\to\your\project" HEAD
```

### Option 2: Build the executable and run it

If you want to compile it into a standalone executable:

```bash
# Build the tool
dotnet build -c Release

# Run the compiled executable directly
.\bin\Release\net8.0\DiffCoverageTool.exe "C:\path\to\your\project" HEAD~1
```

## Troubleshooting

- **"No modified files found"**: This means `git diff -U0 <base-ref>` didn't return any changes. Ensure you have made changes relative to the base reference.
- **"Could not find coverage.cobertura.xml"**: The tool executes `dotnet test --collect:"XPlat Code Coverage"`. If Coverlet doesn't generate this file, make sure your target project actually has tests and references `coverlet.collector` (which is included by default via `dotnet new xunit`).
- **"No coverable new lines found"**: If your coverage file is generated but reports 0 lines, ensure that your application code and your test code are in **separate projects** (e.g., `MyApp.Core` and `MyApp.Tests`). By default, Coverlet excludes the assembly containing the tests from coverage reporting.
