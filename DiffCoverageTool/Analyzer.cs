using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DiffCoverageTool
{
    public class Analyzer
    {
        public static void Analyze(
            Dictionary<string, HashSet<int>> modifiedLines,
            Dictionary<string, Dictionary<int, bool>> coverageData)
        {
            int totalNewLinesToCover = 0;
            int coveredNewLines = 0;

            Console.WriteLine("\n--- New Code Coverage ---");
            foreach (var kvp in modifiedLines)
            {
                string filePath = kvp.Key;
                var lines = kvp.Value;
                
                var coverageKey = coverageData.Keys.FirstOrDefault(k => string.Equals(k, filePath, StringComparison.OrdinalIgnoreCase)) ?? filePath;
                
                int fileLinesToCover = 0;
                int fileLinesCovered = 0;

                if (coverageData.TryGetValue(coverageKey, out var fileCoverage))
                {
                    foreach (var lineNum in lines)
                    {
                        if (fileCoverage.TryGetValue(lineNum, out bool isCovered))
                        {
                            fileLinesToCover++;
                            if (isCovered) fileLinesCovered++;
                        }
                    }
                }

                if (fileLinesToCover > 0)
                {
                    totalNewLinesToCover += fileLinesToCover;
                    coveredNewLines += fileLinesCovered;
                    double filePct = (double)fileLinesCovered / fileLinesToCover * 100;
                    Console.WriteLine($"{Path.GetFileName(filePath)}: {filePct:F2}% ({fileLinesCovered}/{fileLinesToCover} lines)");
                }
            }

            Console.WriteLine("-------------------------");
            if (totalNewLinesToCover > 0)
            {
                double totalPct = (double)coveredNewLines / totalNewLinesToCover * 100;
                Console.WriteLine($"Total New Code Coverage: {totalPct:F2}% ({coveredNewLines}/{totalNewLinesToCover} lines)");
            }
            else
            {
                Console.WriteLine("No coverable new lines found.");
            }
        }
    }
}
