using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace DiffCoverageTool
{
    public class CoverageParser
    {
        public static (Dictionary<string, Dictionary<int, bool>> Coverage, Dictionary<string, string> FileToPackage) ParseCobertura(IEnumerable<string> xmlPaths)
        {
            var coverage = new Dictionary<string, Dictionary<int, bool>>(StringComparer.OrdinalIgnoreCase);
            var fileToPackage = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var xmlPath in xmlPaths)
            {
                var doc = XDocument.Load(xmlPath);

                var packages = doc.Descendants("package");
                foreach (var pkg in packages)
                {
                    string packageName = pkg.Attribute("name")?.Value ?? "Unknown Service";

                    var classes = pkg.Descendants("class");
                    foreach (var cls in classes)
                    {
                        var filenameAttr = cls.Attribute("filename");
                        if (filenameAttr == null) continue;
                        
                        string filename = filenameAttr.Value;
                        
                        if (!Path.IsPathRooted(filename))
                        {
                            var sources = doc.Descendants("source").Select(s => s.Value).ToList();
                            string root = sources.FirstOrDefault() ?? string.Empty;
                            filename = Path.GetFullPath(Path.Combine(root, filename));
                        }
                        
                        filename = filename.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);

                        fileToPackage[filename] = packageName;

                        if (!coverage.ContainsKey(filename))
                        {
                            coverage[filename] = new Dictionary<int, bool>();
                        }
                        
                        var lines = cls.Element("lines")?.Elements("line");
                        if (lines != null)
                        {
                            foreach (var line in lines)
                            {
                                if (int.TryParse(line.Attribute("number")?.Value, out int number) &&
                                    int.TryParse(line.Attribute("hits")?.Value, out int hits))
                                {
                                    bool isCovered = hits > 0;
                                    // Merge coverage across multiple test runs (if covered in any, it's covered)
                                    if (coverage[filename].TryGetValue(number, out bool existingCovered))
                                    {
                                        coverage[filename][number] = existingCovered || isCovered;
                                    }
                                    else
                                    {
                                        coverage[filename][number] = isCovered;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return (coverage, fileToPackage);
        }
    }
}
