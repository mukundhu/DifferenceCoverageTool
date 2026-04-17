using CliWrap;
using CliWrap.Buffered;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DiffCoverageTool.Runners
{
    public class AngularNpmTestRunner : ITestRunner
    {
        private readonly ILogger<AngularNpmTestRunner> _logger;

        public AngularNpmTestRunner(ILogger<AngularNpmTestRunner> logger)
        {
            _logger = logger;
        }

        public async Task<(bool Success, string Output)> RunTestsAsync(string repoPath, HashSet<string> selectedPaths)
        {
            _logger.LogInformation("Scanning for Angular projects natively...");
            
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
            var combinedOutput = new StringBuilder();

            await Parallel.ForEachAsync(runPaths, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, async (executionPath, token) =>
            {
                _logger.LogInformation($"Running npm test inside natively discovered app scope: {executionPath}");
                try
                {
                    var npmCmd = Cli.Wrap("cmd.exe")
                        .WithArguments("/c npm run test -- --no-watch --code-coverage")
                        .WithWorkingDirectory(executionPath)
                        .WithValidation(CommandResultValidation.None);

                    var result = await npmCmd.ExecuteBufferedAsync(token);
                    string procOutput = result.StandardOutput + "\n" + result.StandardError;
                    
                    lock(combinedOutput)
                    {
                        if (result.ExitCode != 0)
                        {
                            allSuccess = false;
                            combinedOutput.AppendLine($"=== FAILED: {executionPath} ===\n{procOutput}\n");
                            _logger.LogWarning($"Angular/NPM tests violently exited inside scope {executionPath}");
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
    }
}
