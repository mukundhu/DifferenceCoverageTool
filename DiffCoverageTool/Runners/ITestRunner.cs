using System.Collections.Generic;
using System.Threading.Tasks;

namespace DiffCoverageTool.Runners
{
    public interface ITestRunner
    {
        Task<(bool Success, string Output)> RunTestsAsync(string repoPath, HashSet<string> selectedPaths);
    }
}
