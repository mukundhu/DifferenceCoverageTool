using System;
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

            repoPath = Path.GetFullPath(repoPath);

            Console.WriteLine($"Analyzing diff coverage in '{repoPath}' against base '{baseRef}'");

            try
            {
                // 1. Run git diff
                var modifiedLines = DiffParser.GetModifiedLines(repoPath, baseRef);
                if (!modifiedLines.Any())
                {
                    Console.WriteLine("No modified files found.");
                    return 0;
                }

                // 2. Run dotnet test
                RunDotnetTest(repoPath);

                // Find coverage.cobertura.xml
                var coverageFiles = Directory.GetFiles(repoPath, "coverage.cobertura.xml", SearchOption.AllDirectories);
                if (coverageFiles.Length == 0)
                {
                    Console.WriteLine("Could not find coverage.cobertura.xml. Ensure tests have coverage enabled.");
                    return 1;
                }
                
                var coverageFile = coverageFiles.OrderByDescending(f => File.GetLastWriteTime(f)).First();
                Console.WriteLine($"Using coverage report: {coverageFile}");

                // 3. Parse Coverage
                var coverageData = CoverageParser.ParseCobertura(coverageFile);

                // 4. Analyze and Output
                Analyzer.Analyze(modifiedLines, coverageData);

                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine(ex.StackTrace);
                return 1;
            }
        }

        static void RunDotnetTest(string repoPath)
        {
            Console.WriteLine("Running dotnet test with coverage...");
            var startInfo = new ProcessStartInfo("dotnet", "test --collect:\"XPlat Code Coverage\"")
            {
                WorkingDirectory = repoPath,
                UseShellExecute = false
            };
            var process = Process.Start(startInfo);
            process.WaitForExit();
            
            if (process.ExitCode != 0)
            {
                Console.WriteLine("Warning: Tests failed. Continuing analysis anyway...");
            }
        }
    }
}
