using CommandLine;

namespace DiffCoverageTool
{
    public class Options
    {
        [Value(0, MetaName = "repoPath", Required = false, HelpText = "Path to the repository. Defaults to current directory.", Default = ".")]
        public string RepoPath { get; set; }

        [Value(1, MetaName = "baseRef", Required = false, HelpText = "Base ref to compare against (e.g. HEAD~1).", Default = "HEAD~1")]
        public string BaseRef { get; set; }

        [Value(2, MetaName = "projectType", Required = false, HelpText = "Type of project (dotnet or angular).", Default = "dotnet")]
        public string ProjectType { get; set; }

        [Value(3, MetaName = "selectedProjects", Required = false, HelpText = "Pipe-separated list of selected projects, or 'ALL'", Default = "ALL")]
        public string SelectedProjects { get; set; }

        [Value(4, MetaName = "reportMode", Required = false, HelpText = "Report mode (detail-only, etc.)", Default = "detail-only")]
        public string ReportMode { get; set; }

        [Option('v', "verbose", Required = false, HelpText = "Set output to verbose messages.")]
        public bool Verbose { get; set; }
    }
}
