const express = require('express');
const cors = require('cors');
const { exec } = require('child_process');
const path = require('path');
const fs = require('fs');

const app = express();
const PORT = 3000;

app.use(cors());
app.use(express.json());
app.use(express.static('public'));

// Endpoint to fetch commits for a given path
app.get('/api/commits', (req, res) => {
    const repoPath = req.query.path;
    if (!repoPath || !fs.existsSync(repoPath)) {
        return res.status(400).json({ error: 'Invalid or missing repository path.' });
    }

    // Run git log to get the last ~50 commits
    const gitCmd = `git log -n 50 --pretty=format:"%H|%s|%ar|%an"`;
    
    exec(gitCmd, { cwd: repoPath }, (error, stdout, stderr) => {
        if (error) {
            console.error(`Error executing git log: ${error.message}`);
            return res.status(500).json({ error: 'Failed to fetch commits. Ensure it is a valid git repository.' });
        }

        const commits = stdout.split('\n').filter(line => line.trim()).map(line => {
            const [hash, message, time, author] = line.split('|');
            return { hash, message, time, author };
        });

        res.json({ commits });
    });
});

// Endpoint to scan for available services/projects in a folder
app.get('/api/discover-services', (req, res) => {
    const { path: repoPath, framework = 'dotnet' } = req.query;
    if (!repoPath || !fs.existsSync(repoPath)) {
        return res.status(400).json({ error: 'Invalid or missing repository path.' });
    }

    const skipDirs = new Set(['node_modules', '.git', 'bin', 'obj', '.vs']);

    const walkDir = (dir, depth = 0) => {
        if (depth > 8) return [];
        let results = [];
        try {
            const entries = fs.readdirSync(dir, { withFileTypes: true });
            for (const entry of entries) {
                if (!entry.isDirectory()) continue;
                if (skipDirs.has(entry.name)) continue;
                const fullPath = path.join(dir, entry.name);
                results = results.concat(walkDir(fullPath, depth + 1));
            }
            // Check this directory itself
            const isTestProject = (name) => /\.(tests?|specs?|unittests?|integrationtests?)$/i.test(name.replace(/\.csproj$/i, ''));
            const files = fs.readdirSync(dir).filter(f => {
                if (framework === 'angular') return f === 'package.json';
                return f.endsWith('.csproj') && !isTestProject(f);
            });
            // For Angular, skip directories that are clearly test-only apps
            const dirName = path.basename(dir).toLowerCase();
            const isTestDir = /tests?$|specs?$|\.tests?$/.test(dirName);
            if (framework === 'angular' && isTestDir) return results;

            for (const f of files) {
                const name = framework === 'angular' ? path.basename(dir) : f.replace('.csproj', '');
                results.push({ name, path: dir, file: f });
            }
        } catch (e) { /* skip permission errors */ }
        return results;
    };

    const services = walkDir(repoPath);
    // Deduplicate by path
    const seen = new Set();
    const unique = services.filter(s => {
        if (seen.has(s.path)) return false;
        seen.add(s.path);
        return true;
    });
    res.json({ services: unique });
});

// Endpoint to run coverage tool
app.post('/api/run-coverage', (req, res) => {
    const { repoPath, baseRef, targetRef, projectType = 'dotnet', reportMode = 'detail-only', selectedProjects = [] } = req.body;
    
    if (!repoPath || !fs.existsSync(repoPath) || !baseRef || !targetRef) {
        return res.status(400).json({ error: 'Invalid payload. Ensure repo path, baseRef, and targetRef are provided.' });
    }

    const csprojPath = path.resolve(__dirname, '../DiffCoverageTool/DiffCoverageTool.csproj');
    
    let diffArg = '';
    if (baseRef === 'FULL_COVERAGE') {
        diffArg = 'FULL_COVERAGE';
    } else {
        diffArg = targetRef === 'UNCOMMITTED' ? baseRef : `${baseRef}..${targetRef}`;
    }

    // selectedProjects: pipe-separated paths, or "ALL" if none selected (run everything)
    const projectsArg = (selectedProjects && selectedProjects.length > 0)
        ? selectedProjects.join('|')
        : 'ALL';

    // args: repoPath diffArg projectType selectedProjects reportMode
    const runCmd = `dotnet run --project "${csprojPath}" "${repoPath}" "${diffArg}" "${projectType}" "${projectsArg}" "${reportMode}"`;

    exec(runCmd, { cwd: repoPath }, (error, stdout, stderr) => {
        // Output might just be warnings or exit code 1 if tests fail, but report might still generate.
        // We will ignore generic errors if the report file exists.
        
        const reportPath = path.join(repoPath, 'coverage_report.html');
        if (fs.existsSync(reportPath)) {
            const htmlContent = fs.readFileSync(reportPath, 'utf8');
            res.json({ success: true, stdout, htmlContent });
        } else {
            console.error(stdout);
            console.error(stderr);
            res.status(500).json({ 
                success: false, 
                error: 'Failed to generate coverage report.',
                details: stdout + '\n' + stderr
            });
        }
    });
});

// Endpoint to aggressively clear locally generated report logs inside repo directories automatically
app.post('/api/clear-reports', (req, res) => {
    try {
        const { repoPath } = req.body;
        
        // Target: Local dashboard HTML artifact
        const localReport = path.resolve(process.cwd(), '../coverage_report.html');
        if (fs.existsSync(localReport)) {
            fs.unlinkSync(localReport);
        }

        // Target: Selected repository cache targets natively via Javascript Recursion securely
        const deleteFolderRecursive = (dirPath) => {
            if (fs.existsSync(dirPath)) {
                const dirents = fs.readdirSync(dirPath, { withFileTypes: true });
                for (const dirent of dirents) {
                    const fullPath = path.join(dirPath, dirent.name);
                    if (dirent.isDirectory()) {
                        if (dirent.name === 'TestResults' || dirent.name === 'coverage') {
                            fs.rmSync(fullPath, { recursive: true, force: true });
                        } else {
                            // strictly exclude node_modules or system stores from intensive recursion
                            if (dirent.name !== 'node_modules' && dirent.name !== '.git' && dirent.name !== 'bin' && dirent.name !== 'obj') {
                                deleteFolderRecursive(fullPath);
                            }
                        }
                    } else if (dirent.isFile()) {
                        // aggressively eliminate any XML traces generated outside the TestResults folders
                        if (dirent.name === 'coverage.cobertura.xml' || dirent.name === 'coverage_report.html') {
                            fs.unlinkSync(fullPath);
                        }
                    }
                }
            }
        };

        if (repoPath && fs.existsSync(repoPath)) {
            deleteFolderRecursive(repoPath);
        }

        res.json({ message: 'All local Dashboard traces and TestResults folders have been physically eliminated.' });
    } catch (error) {
        console.error("Error clearing local footprints:", error);
        res.status(500).json({ error: error.message });
    }
});

app.listen(PORT, () => {
    console.log(`DiffCoverage Dashboard server running on http://localhost:${PORT}`);
});
