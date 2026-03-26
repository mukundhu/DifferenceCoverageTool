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

            repoPath = Path.GetFullPath(repoPath);

            Console.WriteLine($"Analyzing diff coverage in '{repoPath}' against base '{baseRef}'");

            try
            {
                // 1. Run test based on project type
                bool testsPassed = true;
                string testOutput = "";

                if (projectType == "angular")
                {
                    var result = RunAngularTest(repoPath);
                    testsPassed = result.success;
                    testOutput = result.output;
                }
                else
                {
                    var result = RunDotnetTest(repoPath);
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
                HtmlReportGenerator.GenerateReport(modifiedLines, coverageData, fileToPackage, repoPath, testsPassed, testOutput);

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static (bool success, string output) RunDotnetTest(string repoPath)
        {
            Console.WriteLine("Running dotnet test with coverage...");
            string output = "";
            bool success = true;
            try
            {
                var startInfo = new ProcessStartInfo("dotnet", "test --collect:\"XPlat Code Coverage\"")
                {
                    WorkingDirectory = repoPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                var process = Process.Start(startInfo);
                output = process.StandardOutput.ReadToEnd() + "\n" + process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                success = process.ExitCode == 0;
                if (!success)
                {
                    Console.WriteLine("Warning: Tests failed. Continuing analysis anyway...");
                }
            }
            catch (Exception ex)
            {
                success = false;
                output = ex.Message;
            }
            return (success, output);
        }

        static (bool success, string output) RunAngularTest(string repoPath)
        {
            Console.WriteLine("Running angular test with coverage...");
            string output = "";
            bool success = true;
            try
            {
                // On Windows, running npm requires shell execution or invoking cmd.exe
                var startInfo = new ProcessStartInfo("cmd.exe", "/c npm run test -- --no-watch --code-coverage")
                {
                    WorkingDirectory = repoPath,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false
                };
                var process = Process.Start(startInfo);
                output = process.StandardOutput.ReadToEnd() + "\n" + process.StandardError.ReadToEnd();
                process.WaitForExit();
                
                success = process.ExitCode == 0;
                if (!success)
                {
                    Console.WriteLine("Warning: Tests failed. Continuing analysis anyway...");
                }
            }
            catch (Exception ex)
            {
                success = false;
                output = ex.Message;
            }
            return (success, output);
        }
    }
}
