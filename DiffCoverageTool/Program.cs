using CommandLine;
using DiffCoverageTool.Runners;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace DiffCoverageTool
{
    class Program
    {
        static async Task<int> Main(string[] args)
        {
            var parser = new Parser(with => {
                with.HelpWriter = Console.Error;
                with.IgnoreUnknownArguments = true;
            });

            return await parser.ParseArguments<Options>(args)
                .MapResult(
                    async opts => await RunAsync(opts),
                    _ => Task.FromResult(1)
                );
        }

        static async Task<int> RunAsync(Options options)
        {
            using var loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(options.Verbose ? LogLevel.Debug : LogLevel.Information);
            });
            var logger = loggerFactory.CreateLogger<Program>();

            string repoPath = Path.GetFullPath(options.RepoPath ?? ".");
            string baseRef = options.BaseRef ?? "HEAD~1";
            string projectType = (options.ProjectType ?? "dotnet").ToLower();
            string selectedProjectsArg = options.SelectedProjects ?? "ALL";
            string reportMode = (options.ReportMode ?? "detail-only").ToLower();

            HashSet<string> selectedPaths = selectedProjectsArg == "ALL" ? null : new HashSet<string>(
                selectedProjectsArg.Split('|', StringSplitOptions.RemoveEmptyEntries), StringComparer.OrdinalIgnoreCase);

            logger.LogInformation($"Analyzing diff coverage in '{repoPath}' against base '{baseRef}'");
            if (selectedPaths != null)
                logger.LogInformation($"Running for {selectedPaths.Count} selected service(s).");

            try
            {
                // 1. Run Tests
                bool testsPassed = true;
                string testOutput = "";
                
                ITestRunner runner;
                if (projectType == "angular")
                {
                    runner = new AngularNpmTestRunner(loggerFactory.CreateLogger<AngularNpmTestRunner>());
                }
                else
                {
                    runner = new DotnetTestRunner(loggerFactory.CreateLogger<DotnetTestRunner>());
                }

                var runResult = await runner.RunTestsAsync(repoPath, selectedPaths);
                testsPassed = runResult.Success;
                testOutput = runResult.Output;

                // Find coverage.cobertura.xml
                var coverageFiles = Directory.GetFiles(repoPath, "coverage.cobertura.xml", SearchOption.AllDirectories);
                if (coverageFiles.Length == 0)
                    coverageFiles = Directory.GetFiles(repoPath, "coverage.xml", SearchOption.AllDirectories);

                if (coverageFiles.Length == 0)
                {
                    logger.LogError("Could not find coverage.cobertura.xml. Ensure tests have the 'coverlet.collector' NuGet package installed.");
                    var testResultsDirs = Directory.GetDirectories(repoPath, "TestResults", SearchOption.AllDirectories);
                    if (testResultsDirs.Length == 0)
                    {
                        logger.LogError("No TestResults directories were found — test runner may not have run successfully.");
                    }
                    else
                    {
                        logger.LogInformation("TestResults directories found (but no coverage.cobertura.xml inside):");
                        foreach (var dir in testResultsDirs)
                        {
                            logger.LogInformation($"  {dir}");
                        }
                    }
                    return 1;
                }
                
                logger.LogInformation($"Found {coverageFiles.Length} coverage reports. Merging records...");

                // 2. Parse Coverage
                var parsed = CoverageParser.ParseCobertura(coverageFiles);
                var coverageData = parsed.Coverage;
                var fileToPackage = parsed.FileToPackage;

                Dictionary<string, HashSet<int>> modifiedLines = new Dictionary<string, HashSet<int>>();

                // 3. Parse git diff or bypass
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
                        logger.LogInformation("No modified files found.");
                        return 0;
                    }
                }

                // 4. Analyze and Output
                Analyzer.Analyze(modifiedLines, coverageData);

                // 5. Generate HTML Report
                HtmlReportGenerator.GenerateReport(modifiedLines, coverageData, fileToPackage, repoPath, testsPassed, testOutput, reportMode);

                logger.LogInformation("DiffCoverage Tool execution completed successfully.");
                return 0;
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "A critical error occurred during execution.");
                return 1;
            }
        }
    }
}
