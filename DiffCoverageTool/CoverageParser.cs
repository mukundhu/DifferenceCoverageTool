using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace DiffCoverageTool
{
    public class CoverageParser
    {
        public static Dictionary<string, Dictionary<int, bool>> ParseCobertura(string xmlPath)
        {
            var coverage = new Dictionary<string, Dictionary<int, bool>>(StringComparer.OrdinalIgnoreCase);
            var doc = XDocument.Load(xmlPath);

            var classes = doc.Descendants("class");
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
                            coverage[filename][number] = hits > 0;
                        }
                    }
                }
            }
            return coverage;
        }
    }
}
