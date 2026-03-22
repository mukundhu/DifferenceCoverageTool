using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace DiffCoverageTool
{
    public class DiffParser
    {
        public static Dictionary<string, HashSet<int>> GetModifiedLines(string repoPath, string baseRef)
        {
            var modifiedLines = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

            // Get git root
            var startInfo = new ProcessStartInfo("git", "rev-parse --show-toplevel")
            {
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var process = Process.Start(startInfo);
            string gitRoot = process.StandardOutput.ReadLine().Trim();
            process.WaitForExit();
            
            // Normalize git root to OS path format
            gitRoot = Path.GetFullPath(gitRoot);

            // Run git diff
            var diffStartInfo = new ProcessStartInfo("git", $"diff -U0 {baseRef}")
            {
                WorkingDirectory = repoPath,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            var diffProcess = Process.Start(diffStartInfo);
            
            string currentFile = null;
            while (!diffProcess.StandardOutput.EndOfStream)
            {
                var line = diffProcess.StandardOutput.ReadLine();
                if (line.StartsWith("+++ b/"))
                {
                    currentFile = Path.Combine(gitRoot, line.Substring(6)).Replace('/', Path.DirectorySeparatorChar);
                    if (!modifiedLines.ContainsKey(currentFile))
                    {
                        modifiedLines[currentFile] = new HashSet<int>();
                    }
                }
                else if (line.StartsWith("@@ ") && currentFile != null)
                {
                    // e.g., @@ -1,2 +1,2 @@
                    var parts = line.Split(' ');
                    if (parts.Length > 2)
                    {
                        var newLinesPart = parts[2]; // e.g., +1,2 or +1
                        
                        var newLinesTokens = newLinesPart.Substring(1).Split(',');
                        if (int.TryParse(newLinesTokens[0], out int startLine))
                        {
                            int count = newLinesTokens.Length > 1 ? int.Parse(newLinesTokens[1]) : 1;
                            
                            for (int i = 0; i < count; i++)
                            {
                                modifiedLines[currentFile].Add(startLine + i);
                            }
                        }
                    }
                }
            }
            diffProcess.WaitForExit();

            return modifiedLines;
        }
    }
}
