using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace DiffCoverageTool
{
    public class HtmlReportGenerator
    {
        public static void GenerateReport(
            Dictionary<string, HashSet<int>> modifiedLines,
            Dictionary<string, Dictionary<int, bool>> coverageData,
            Dictionary<string, string> fileToPackage,
            string repoPath,
            bool testsPassed = true,
            string testOutput = "")
        {
            if (!modifiedLines.Any())
            {
                Console.WriteLine("No modified files to report.");
                return;
            }

            string reportPath = Path.Combine(Directory.GetCurrentDirectory(), "coverage_report.html");
            var sb = new StringBuilder();

            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html lang=\"en\">");
            sb.AppendLine("<head>");
            sb.AppendLine("    <meta charset=\"UTF-8\">");
            sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
            sb.AppendLine("    <title>Diff Coverage Report</title>");
            sb.AppendLine("    <style>");
            sb.AppendLine("        body { font-family: -apple-system, BlinkMacSystemFont, \"Segoe UI\", Roboto, Helvetica, Arial, sans-serif; margin: 0; padding: 20px; background-color: #f8f9fa; color: #333; }");
            sb.AppendLine("        h1 { color: #24292e; border-bottom: 1px solid #eaecef; padding-bottom: 10px; }");
            sb.AppendLine("        .file-section { background: #fff; border: 1px solid #d1d5da; border-radius: 6px; margin-bottom: 20px; overflow: hidden; }");
            sb.AppendLine("        .file-header { background: #f6f8fa; padding: 10px 15px; font-weight: 600; border-bottom: 1px solid #d1d5da; font-family: monospace; }");
            sb.AppendLine("        .code-table { width: 100%; border-collapse: collapse; font-family: SFMono-Regular, Consolas, \"Liberation Mono\", Menlo, monospace; font-size: 12px; }");
            sb.AppendLine("        .code-table td { padding: 2px 10px; white-space: pre-wrap; word-wrap: break-word; }");
            sb.AppendLine("        .line-num { width: 40px; text-align: right; color: #959da5; border-right: 1px solid #eaecef; user-select: none; }");
            sb.AppendLine("        .line-code { padding-left: 15px; }");
            sb.AppendLine("        .covered { background-color: #e6ffed; }");
            sb.AppendLine("        .uncovered { background-color: #ffeef0; }");
            sb.AppendLine("        .unmodified { background-color: #ffffff; color: #586069; }");
            sb.AppendLine("        .text-covered { color: #28a745; }");
            sb.AppendLine("        .text-uncovered { color: #cb2431; }");
            sb.AppendLine("        .tabs { display: flex; border-bottom: 2px solid #eaecef; margin-bottom: 20px; }");
            sb.AppendLine("        .tab-btn { padding: 10px 20px; cursor: pointer; border: none; background: transparent; font-size: 16px; font-weight: bold; color: #586069; }");
            sb.AppendLine("        .tab-btn.active { color: #24292e; border-bottom: 2px solid #0366d6; margin-bottom: -2px; }");
            sb.AppendLine("        .tab-content { display: none; }");
            sb.AppendLine("        .tab-content.active { display: block; }");
            sb.AppendLine("        .service-table { width: 100%; border-collapse: collapse; margin-bottom: 20px; background: white; border-radius: 6px; overflow: hidden; box-shadow: 0 1px 3px rgba(0,0,0,0.1); }");
            sb.AppendLine("        .service-table th, .service-table td { padding: 12px 15px; text-align: left; border-bottom: 1px solid #eaecef; }");
            sb.AppendLine("        .service-table th { background-color: #f6f8fa; font-weight: 600; color: #24292e; }");
            sb.AppendLine("        @media print {");
            sb.AppendLine("            .tabs, .summary, .no-print, #fileTab { min-height: 0 !important; display: none !important; }");
            sb.AppendLine("            #summaryTab { display: block !important; }");
            sb.AppendLine("            body { background: white; padding: 0; }");
            sb.AppendLine("            h1 { font-size: 24pt; border-bottom: none; text-align: center; margin-bottom: 30px; }");
            sb.AppendLine("            .service-table { box-shadow: none; border: 1px solid #ccc; }");
            sb.AppendLine("            .service-table th { -webkit-print-color-adjust: exact; background-color: #f0f0f0 !important; }");
            sb.AppendLine("        }");
            sb.AppendLine("    </style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");

            int totalModifiedCoverable = 0;
            int totalCovered = 0;
            var serviceStats = new Dictionary<string, (int coverable, int covered)>();

            // Pre-calculate totals for summary
            foreach (var kvp in modifiedLines)
            {
                string filePath = kvp.Key;
                var coverageKey = coverageData.Keys.FirstOrDefault(k => string.Equals(k, filePath, StringComparison.OrdinalIgnoreCase)) ?? filePath;
                
                string serviceName = "Unknown Service";
                if (fileToPackage.TryGetValue(coverageKey, out var pkgName)) serviceName = pkgName;

                if (!serviceStats.ContainsKey(serviceName)) serviceStats[serviceName] = (0, 0);

                if (coverageData.TryGetValue(coverageKey, out var fileCoverage))
                {
                    int sCoverable = 0;
                    int sCovered = 0;
                    foreach (var lineNum in kvp.Value)
                    {
                        if (fileCoverage.ContainsKey(lineNum))
                        {
                            totalModifiedCoverable++;
                            sCoverable++;
                            
                            if (fileCoverage[lineNum]) 
                            {
                                totalCovered++;
                                sCovered++;
                            }
                        }
                    }
                    var current = serviceStats[serviceName];
                    serviceStats[serviceName] = (current.coverable + sCoverable, current.covered + sCovered);
                }
            }

            sb.AppendLine("    <h1>Diff Coverage Report</h1>");

            if (!testsPassed && !string.IsNullOrWhiteSpace(testOutput))
            {
                sb.AppendLine("    <div style=\"background-color: #ffeef0; border: 1px solid #cb2431; padding: 15px; border-radius: 6px; margin-bottom: 20px;\">");
                sb.AppendLine("        <h2 style=\"color: #cb2431; margin-top: 0;\">⚠️ Test Failures Detected</h2>");
                sb.AppendLine("        <p>The test runner returned a non-zero exit code. Here is the console output for debugging:</p>");
                sb.AppendLine("        <pre style=\"background-color: #24292e; color: #f6f8fa; padding: 15px; border-radius: 6px; overflow-x: auto; max-height: 400px;\">");
                sb.AppendLine(System.Net.WebUtility.HtmlEncode(testOutput));
                sb.AppendLine("        </pre>");
                sb.AppendLine("    </div>");
            }

            sb.AppendLine("    <div class=\"summary\">");
            sb.AppendLine($"        <span>Total Coverable Modified Lines: {totalModifiedCoverable}</span>");
            sb.AppendLine($"        <span class=\"text-covered\">Covered: {totalCovered}</span>");
            sb.AppendLine($"        <span class=\"text-uncovered\">Uncovered: {totalModifiedCoverable - totalCovered}</span>");
            
            if (totalModifiedCoverable > 0)
            {
                double pct = (double)totalCovered / totalModifiedCoverable * 100;
                sb.AppendLine($"        <span>Coverage: {pct:F2}%</span>");
            }
            sb.AppendLine("    </div>");

            sb.AppendLine("    <div class=\"tabs no-print\">");
            sb.AppendLine("        <button class=\"tab-btn active\" onclick=\"switchTab('fileTab', this)\">File Detail View</button>");
            sb.AppendLine("        <button class=\"tab-btn\" onclick=\"switchTab('summaryTab', this)\">Project / Service Summary</button>");
            sb.AppendLine("    </div>");

            sb.AppendLine("    <div id=\"summaryTab\" class=\"tab-content\">");
            sb.AppendLine("        <div style=\"display: flex; justify-content: space-between; align-items: center; margin-bottom: 15px;\">");
            sb.AppendLine("            <h2 style=\"margin: 0;\">Service Coverage Overview</h2>");
            sb.AppendLine("            <button class=\"no-print\" onclick=\"window.print()\" style=\"padding: 8px 16px; background: #0366d6; color: white; border: none; border-radius: 4px; cursor: pointer; font-weight: bold;\">Export to PDF</button>");
            sb.AppendLine("        </div>");
            sb.AppendLine("        <table class=\"service-table\">");
            sb.AppendLine("            <thead><tr><th>Project / Service</th><th>Coverable Lines</th><th>Covered Lines</th><th>Coverage %</th></tr></thead>");
            sb.AppendLine("            <tbody>");
            foreach (var stat in serviceStats.OrderByDescending(s => s.Value.coverable))
            {
                double pct = stat.Value.coverable > 0 ? (double)stat.Value.covered / stat.Value.coverable * 100 : 0;
                string sColor = pct == 100 ? "#28a745" : (pct >= 50 ? "#d97706" : "#cb2431");
                sb.AppendLine("                <tr>");
                sb.AppendLine($"                    <td style=\"font-weight: 600;\">{System.Net.WebUtility.HtmlEncode(stat.Key)}</td>");
                sb.AppendLine($"                    <td>{stat.Value.coverable}</td>");
                sb.AppendLine($"                    <td>{stat.Value.covered}</td>");
                sb.AppendLine($"                    <td style=\"color: {sColor}; font-weight: bold;\">{pct:F2}%</td>");
                sb.AppendLine("                </tr>");
            }
            sb.AppendLine("            </tbody>");
            sb.AppendLine("        </table>");
            sb.AppendLine("    </div>");

            sb.AppendLine("    <div id=\"fileTab\" class=\"tab-content active\">");
            sb.AppendLine("    <div style=\"margin-bottom: 20px;\">");
            sb.AppendLine("        <label for=\"fileSearch\" style=\"font-weight: bold; margin-right: 10px;\">Search / Filter File:</label>");
            sb.AppendLine("        <input list=\"file-list\" id=\"fileSearch\" placeholder=\"Type to search a file...\" style=\"padding: 8px; width: 300px; border: 1px solid #d1d5da; border-radius: 4px;\">");
            sb.AppendLine("        <datalist id=\"file-list\">");
            foreach (var kvp in modifiedLines)
            {
                string filePath = kvp.Key;
                string relativePath = filePath;
                if (filePath.StartsWith(repoPath, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = filePath.Substring(repoPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }
                sb.AppendLine($"            <option value=\"{relativePath}\"></option>");
            }
            sb.AppendLine("        </datalist>");
            sb.AppendLine("        <button onclick=\"document.getElementById('fileSearch').value=''; filterFiles();\" style=\"padding: 8px 12px; margin-left: 5px; cursor: pointer; border: 1px solid #d1d5da; border-radius: 4px; background: white;\">Clear</button>");
            sb.AppendLine("    </div>");

            foreach (var kvp in modifiedLines)
            {
                string filePath = kvp.Key;
                var lines = kvp.Value;
                var coverageKey = coverageData.Keys.FirstOrDefault(k => string.Equals(k, filePath, StringComparison.OrdinalIgnoreCase)) ?? filePath;
                
                Dictionary<int, bool> fileCoverage = null;
                coverageData.TryGetValue(coverageKey, out fileCoverage);

                string relativePath = filePath;
                if (filePath.StartsWith(repoPath, StringComparison.OrdinalIgnoreCase))
                {
                    relativePath = filePath.Substring(repoPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                }

                int fileCoverable = 0;
                int fileCovered = 0;
                if (fileCoverage != null)
                {
                    foreach (var lineNum in lines)
                    {
                        if (fileCoverage.ContainsKey(lineNum))
                        {
                            fileCoverable++;
                            if (fileCoverage[lineNum]) fileCovered++;
                        }
                    }
                }

                string fileStats = "";
                if (fileCoverable > 0)
                {
                    double filePct = (double)fileCovered / fileCoverable * 100;
                    string color = filePct == 100 ? "#28a745" : (filePct >= 50 ? "#d97706" : "#cb2431");
                    fileStats = $"<span style=\"float: right; color: {color};\">{filePct:F2}% ({fileCovered}/{fileCoverable} lines)</span>";
                }
                else
                {
                    fileStats = $"<span style=\"float: right; color: #94a3b8;\">No coverable lines</span>";
                }

                sb.AppendLine("    <div class=\"file-section\">");
                sb.AppendLine($"        <div class=\"file-header\">{relativePath} {fileStats}</div>");
                sb.AppendLine("        <table class=\"code-table\">");
                sb.AppendLine("            <tbody>");

                if (File.Exists(filePath))
                {
                    string[] fileContent = File.ReadAllLines(filePath);
                    for (int i = 0; i < fileContent.Length; i++)
                    {
                        int lineNum = i + 1;
                        string lineText = System.Net.WebUtility.HtmlEncode(fileContent[i]);
                        string cssClass = "unmodified";

                        if (lines.Contains(lineNum))
                        {
                            if (fileCoverage != null && fileCoverage.TryGetValue(lineNum, out bool isCovered))
                            {
                                cssClass = isCovered ? "covered" : "uncovered";
                            }
                            else
                            {
                                // Modified but not coverable (e.g. comments, braces), could use a different color but keep it unmodified for now
                                cssClass = "unmodified"; 
                            }
                        }

                        // Only render lines in a reasonable vicinity of modified lines to save space, 
                        // or just render the whole file. For simplicity and full context, we render the whole file.
                        sb.AppendLine($"                <tr class=\"{cssClass}\">");
                        sb.AppendLine($"                    <td class=\"line-num\">{lineNum}</td>");
                        sb.AppendLine($"                    <td class=\"line-code\">{lineText}</td>");
                        sb.AppendLine("                </tr>");
                    }
                }
                else
                {
                    sb.AppendLine("                <tr><td colspan=\"2\">File not found on disk.</td></tr>");
                }

                sb.AppendLine("            </tbody>");
                sb.AppendLine("        </table>");
                sb.AppendLine("    </div>");
            }
            sb.AppendLine("    </div>"); // close fileTab

            sb.AppendLine("    <script>");
            sb.AppendLine("        function switchTab(tabId, btn) {");
            sb.AppendLine("            document.querySelectorAll('.tab-content').forEach(t => t.classList.remove('active'));");
            sb.AppendLine("            document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));");
            sb.AppendLine("            document.getElementById(tabId).classList.add('active');");
            sb.AppendLine("            btn.classList.add('active');");
            sb.AppendLine("        }");
            sb.AppendLine("        function filterFiles() {");
            sb.AppendLine("            var input = document.getElementById('fileSearch').value.toLowerCase();");
            sb.AppendLine("            var sections = document.getElementsByClassName('file-section');");
            sb.AppendLine("            for (var i = 0; i < sections.length; i++) {");
            sb.AppendLine("                var header = sections[i].getElementsByClassName('file-header')[0];");
            sb.AppendLine("                if (header) {");
            sb.AppendLine("                    var text = header.innerText || header.textContent;");
            sb.AppendLine("                    if (text.toLowerCase().indexOf(input) > -1 || input === '') {");
            sb.AppendLine("                        sections[i].style.display = '';");
            sb.AppendLine("                    } else {");
            sb.AppendLine("                        sections[i].style.display = 'none';");
            sb.AppendLine("                    }");
            sb.AppendLine("                }");
            sb.AppendLine("            }");
            sb.AppendLine("        }");
            sb.AppendLine("        document.getElementById('fileSearch').addEventListener('input', filterFiles);");
            sb.AppendLine("    </script>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");

            File.WriteAllText(reportPath, sb.ToString());
            Console.WriteLine($"\nHTML Coverage Report generated at: {reportPath}");
        }
    }
}
