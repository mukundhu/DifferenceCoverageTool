using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace DiffCoverageTool
{
    class Program
    {
        static int Main(string[] args)
        {
            string repoPath = args.Length > 0 ? args[0] : Directory.GetCurrentDirectory();
            string baseRef = args.Length > 1 ? args[1] : "HEAD~1";
            string projectType = args.Length > 2 ? args[2].ToLower() : "dotnet";
            // args[3] = pipe-separated selected project paths (or "ALL")
            // args[4] = reportMode
            string selectedProjectsArg = args.Length > 3 ? args[3] : "ALL";
            string reportMode = args.Length > 4 ? args[4].ToLower() : "detail-only";

            HashSet<string> selectedPaths = selectedProjectsArg == "ALL"
                ? null  // null means run everything
                : new HashSet<string>(
                    selectedProjectsArg.Split('|', StringSplitOptions.RemoveEmptyEntries),
                    StringComparer.OrdinalIgnoreCase);

            repoPath = Path.GetFullPath(repoPath);

            Console.WriteLine($"Analyzing diff coverage in '{repoPath}' against base '{baseRef}'");
            if (selectedPaths != null)
                Console.WriteLine($"Running for {selectedPaths.Count} selected service(s).");

            try
            {
                // 1. Run test based on project type
                bool testsPassed = true;
                string testOutput = "";

                if (projectType == "angular")
                {
                    var result = RunAngularTest(repoPath, selectedPaths);
                    testsPassed = result.success;
                    testOutput = result.output;
                }
                else
                {
                    var result = RunDotnetTest(repoPath, selectedPaths);
                    testsPassed = result.success;
                    testOutput = result.output;
                }

                // Find coverage.cobertura.xml
                var coverageFiles = Directory.GetFiles(repoPath, "coverage.cobertura.xml", SearchOption.AllDirectories);
                if (coverageFiles.Length == 0)
                {
                    Console.WriteLine("Could not find coverage.cobertura.xml. Ensure tests have coverage enabled.");
                    return 1;
                }
                
                Console.WriteLine($"Found {coverageFiles.Length} coverage reports. Merging records...");

                // 2. Parse Coverage
                var parsed = CoverageParser.ParseCobertura(coverageFiles);
                var coverageData = parsed.Coverage;
                var fileToPackage = parsed.FileToPackage;

                Dictionary<string, HashSet<int>> modifiedLines = new Dictionary<string, HashSet<int>>();

                // 3. Parse git diff or bypass for full coverage
                if (baseRef == "FULL_COVERAGE")
                {
                    foreach (var kvp in coverageData)
                    {
                        modifiedLines[kvp.Key] = new HashSet<int>(kvp.Value.Keys);
                    }
                }
                else
                {
                    modifiedLines = DiffParser.GetModifiedLines(repoPath, baseRef);
                    if (!modifiedLines.Any())
                    {
                        Console.WriteLine("No modified files found.");
                        return 0;
                    }
                }

                // 4. Analyze and Output
                Analyzer.Analyze(modifiedLines, coverageData);

                // 5. Generate HTML Report
                HtmlReportGenerator.GenerateReport(modifiedLines, coverageData, fileToPackage, repoPath, testsPassed, testOutput, reportMode);

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static (bool success, string output) RunDotnetTest(string repoPath, HashSet<string> selectedPaths = null)
        {
            Console.WriteLine("Scanning for .NET projects/solutions...");
            
            List<string> runPaths = new List<string>();

            // Only use the .sln shortcut when the user hasn't made a specific service selection.
            // If selectedPaths is set, we must scan for individual .csproj dirs so paths can match.
            var slnFiles = Directory.GetFiles(repoPath, "*.sln", SearchOption.TopDirectoryOnly);
            if (slnFiles.Length > 0 && selectedPaths == null)
            {
                runPaths.Add(repoPath);
            }
            else
            {
                var csprojFiles = Directory.GetFiles(repoPath, "*test*.csproj", SearchOption.AllDirectories);
                if (csprojFiles.Length == 0)
                    csprojFiles = Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories);
                
                foreach (var proj in csprojFiles)
                    runPaths.Add(Path.GetFullPath(Path.GetDirectoryName(proj)));
                runPaths = runPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            // Filter: normalize selected paths too so case/trailing-slash differences don't cause mismatches
            if (selectedPaths != null && selectedPaths.Count > 0)
            {
                var normalizedSelected = new HashSet<string>(
                    selectedPaths.Select(p => Path.GetFullPath(p)),
                    StringComparer.OrdinalIgnoreCase);
                runPaths = runPaths.Where(p => normalizedSelected.Contains(Path.GetFullPath(p))).ToList();
            }

            if (runPaths.Count == 0)
                return (false, "No matching projects found for the selected services.");

            bool allSuccess = true;
            string combinedOutput = "";

            foreach (var executionPath in runPaths)
            {
                Console.WriteLine($"Running dotnet test in: {executionPath}");
                try
                {
                    var startInfo = new ProcessStartInfo("dotnet", "test --collect:\"XPlat Code Coverage\"")
                    {
                        WorkingDirectory = executionPath,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    };
                    var process = Process.Start(startInfo);
                    string procOutput = process.StandardOutput.ReadToEnd() + "\n" + process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    
                    if (process.ExitCode != 0)
                    {
                        allSuccess = false;
                        combinedOutput += $"=== FAILED: {executionPath} ===\n{procOutput}\n\n";
                        Console.WriteLine($"Warning: Tests failed in {executionPath}");
                    }
                }
                catch (Exception ex)
                {
                    allSuccess = false;
                    combinedOutput += $"=== ERROR: {executionPath} ===\n{ex.Message}\n\n";
                }
            }

            return (allSuccess, combinedOutput);
        }

        static (bool success, string output) RunAngularTest(string repoPath, HashSet<string> selectedPaths = null)
        {
            Console.WriteLine("Scanning for Angular projects natively...");
            
            List<string> runPaths = new List<string>();
            var packageJsons = Directory.GetFiles(repoPath, "package.json", SearchOption.AllDirectories)
                                        .Where(p => !p.Replace('\\', '/').Contains("/node_modules/")).ToList();
            
            if (packageJsons.Any(p => Path.GetDirectoryName(p).Equals(repoPath, StringComparison.OrdinalIgnoreCase))
                && selectedPaths == null)
            {
                runPaths.Add(Path.GetFullPath(repoPath));
            }
            else
            {
                foreach (var pkg in packageJsons)
                    runPaths.Add(Path.GetFullPath(Path.GetDirectoryName(pkg)));
                runPaths = runPaths.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }

            // Filter: normalize selected paths so case/separator differences don't cause mismatches
            if (selectedPaths != null && selectedPaths.Count > 0)
            {
                var normalizedSelected = new HashSet<string>(
                    selectedPaths.Select(p => Path.GetFullPath(p)),
                    StringComparer.OrdinalIgnoreCase);
                runPaths = runPaths.Where(p => normalizedSelected.Contains(Path.GetFullPath(p))).ToList();
            }

            if (runPaths.Count == 0)
                return (false, "No matching Angular projects found for the selected services.");

            bool allSuccess = true;
            string combinedOutput = "";

            foreach (var executionPath in runPaths)
            {
                Console.WriteLine($"Running npm test inside natively discovered app scope: {executionPath}");
                try
                {
                    var startInfo = new ProcessStartInfo("cmd.exe", "/c npm run test -- --no-watch --code-coverage")
                    {
                        WorkingDirectory = executionPath,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = false
                    };
                    var process = Process.Start(startInfo);
                    string procOutput = process.StandardOutput.ReadToEnd() + "\n" + process.StandardError.ReadToEnd();
                    process.WaitForExit();
                    
                    if (process.ExitCode != 0)
                    {
                        allSuccess = false;
                        combinedOutput += $"=== FAILED: {executionPath} ===\n{procOutput}\n\n";
                        Console.WriteLine($"Warning: Angular/NPM tests violently exited inside scope {executionPath}");
                    }
                }
                catch (Exception ex)
                {
                    allSuccess = false;
                    combinedOutput += $"=== ERROR: {executionPath} ===\n{ex.Message}\n\n";
                }
            }

            return (allSuccess, combinedOutput);
        }
    }
}
