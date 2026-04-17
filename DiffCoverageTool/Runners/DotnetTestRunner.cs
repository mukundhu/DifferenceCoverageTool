using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace DiffCoverageTool.Runners
{
    public class DotnetTestRunner : ITestRunner
    {
        private readonly ILogger<DotnetTestRunner> _logger;

        public DotnetTestRunner(ILogger<DotnetTestRunner> logger)
        {
            _logger = logger;
        }

        public async Task<(bool Success, string Output)> RunTestsAsync(string repoPath, HashSet<string> selectedPaths)
        {
            _logger.LogInformation("Scanning for .NET test projects...");

            var allTestCsprojs = Directory.GetFiles(repoPath, "*.csproj", SearchOption.AllDirectories)
                .Where(f => !f.Replace('\\', '/').Contains("/obj/") && !f.Replace('\\', '/').Contains("/bin/"))
                .Where(IsTestCsproj)
                .ToList();

            if (allTestCsprojs.Count == 0)
            {
                return (false, "No test projects found. Ensure test projects reference xunit, nunit, mstest, or Microsoft.NET.Test.Sdk.");
            }

            var allTestDirs = allTestCsprojs
                .Select(f => Path.GetFullPath(Path.GetDirectoryName(f)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            List<string> runPaths;

            if (selectedPaths == null || selectedPaths.Count == 0)
            {
                runPaths = allTestDirs;
            }
            else
            {
                var normalizedSelected = selectedPaths.Select(p => Path.GetFullPath(p)).ToList();
                runPaths = allTestDirs.Where(testDir =>
                {
                    string testDirName = Path.GetFileName(testDir);
                    return normalizedSelected.Any(svcPath =>
                    {
                        string svcName = Path.GetFileName(svcPath);
                        return testDirName.StartsWith(svcName, StringComparison.OrdinalIgnoreCase)
                            || testDirName.Contains(svcName, StringComparison.OrdinalIgnoreCase);
                    });
                }).ToList();

                if (runPaths.Count == 0)
                {
                    _logger.LogWarning("No test projects matched the selected services by name. Running all test projects.");
                    runPaths = allTestDirs;
                }
            }

            _logger.LogInformation($"Found {runPaths.Count} test project(s) to run.");

            bool allSuccess = true;
            var combinedOutput = new StringBuilder();

            await Parallel.ForEachAsync(runPaths, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount }, async (executionPath, token) =>
            {
                _logger.LogInformation($"Running dotnet test in: {executionPath}");
                try
                {
                    var (exitedOk, procOutput) = await RunDotnetTestForPathAsync(executionPath, token);
                    lock(combinedOutput)
                    {
                        if (!exitedOk)
                        {
                            allSuccess = false;
                            combinedOutput.AppendLine($"=== FAILED: {executionPath} ===\n{procOutput}\n");
                            _logger.LogWarning($"Tests failed in {executionPath}");
                        }
                        else
                        {
                            combinedOutput.AppendLine($"=== SUCCESS: {executionPath} ===\n{procOutput}\n");
                        }
                    }
                }
                catch (Exception ex)
                {
                    lock(combinedOutput)
                    {
                        allSuccess = false;
                        combinedOutput.AppendLine($"=== ERROR: {executionPath} ===\n{ex.Message}\n");
                    }
                }
            });

            return (allSuccess, combinedOutput.ToString());
        }

        private bool IsTestCsproj(string csprojPath)
        {
            string name = Path.GetFileNameWithoutExtension(csprojPath);
            if (Regex.IsMatch(name, @"\.(tests?|specs?|unittests?|integrationtests?)$", RegexOptions.IgnoreCase))
                return true;
            try
            {
                string content = File.ReadAllText(csprojPath);
                return content.Contains("xunit", StringComparison.OrdinalIgnoreCase)
                    || content.Contains("nunit", StringComparison.OrdinalIgnoreCase)
                    || content.Contains("mstest", StringComparison.OrdinalIgnoreCase)
                    || content.Contains("Microsoft.NET.Test.Sdk", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        private async Task<(bool success, string output)> RunDotnetTestForPathAsync(string executionPath, System.Threading.CancellationToken token)
        {
            string resultsDir = Path.Combine(executionPath, "TestResults");
            Directory.CreateDirectory(resultsDir);

            _logger.LogInformation($"  [{Path.GetFileName(executionPath)}] Trying dotnet test --collect:\"XPlat Code Coverage\"...");
            
            var xplatCmd = Cli.Wrap("dotnet")
                .WithArguments($"test --collect:\"XPlat Code Coverage\" --results-directory \"{resultsDir}\" -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura")
                .WithWorkingDirectory(executionPath)
                .WithValidation(CommandResultValidation.None);

            var xplatResult = await xplatCmd.ExecuteBufferedAsync(token);
            string xplatOutput = xplatResult.StandardOutput + "\n" + xplatResult.StandardError;

            var xmlFiles = Directory.GetFiles(resultsDir, "coverage.cobertura.xml", SearchOption.AllDirectories);
            if (xmlFiles.Length > 0)
            {
                _logger.LogInformation($"  [{Path.GetFileName(executionPath)}] XPlat Code Coverage succeeded.");
                return (xplatResult.ExitCode == 0, xplatOutput);
            }

            _logger.LogInformation($"  [{Path.GetFileName(executionPath)}] XPlat did not produce coverage.cobertura.xml. Falling back to dotnet-coverage collect...");
            
            string coberturaOut = Path.Combine(resultsDir, "coverage.cobertura.xml");
            var dcCmd = Cli.Wrap("cmd.exe")
                .WithArguments($"/c dotnet-coverage collect --output \"{coberturaOut}\" --output-format cobertura dotnet test")
                .WithWorkingDirectory(executionPath)
                .WithValidation(CommandResultValidation.None);

            var dcResult = await dcCmd.ExecuteBufferedAsync(token);
            string dcOutput = dcResult.StandardOutput + "\n" + dcResult.StandardError;

            return (dcResult.ExitCode == 0, xplatOutput + "\n" + dcOutput);
        }
    }
}
