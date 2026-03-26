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

// Endpoint to run coverage tool
app.post('/api/run-coverage', (req, res) => {
    const { repoPath, baseRef, targetRef, projectType = 'dotnet' } = req.body;
    
    if (!repoPath || !fs.existsSync(repoPath) || !baseRef || !targetRef) {
        return res.status(400).json({ error: 'Invalid payload. Ensure repo path, baseRef, and targetRef are provided.' });
    }

    // Determine the DiffCoverageTool CSPROJ path relative to the dashboard directory
    const csprojPath = path.resolve(__dirname, '../DiffCoverageTool/DiffCoverageTool.csproj');
    
    // Construct the diff parameter: baseHash or baseHash..targetHash
    let diffArg = '';
    if (baseRef === 'FULL_COVERAGE') {
        diffArg = 'FULL_COVERAGE';
    } else {
        diffArg = targetRef === 'UNCOMMITTED' ? baseRef : `${baseRef}..${targetRef}`;
    }

    // Command: dotnet run --project <csproj> <repoPath> <diffArg> <projectType>
    const runCmd = `dotnet run --project "${csprojPath}" "${repoPath}" "${diffArg}" "${projectType}"`;

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

app.listen(PORT, () => {
    console.log(`DiffCoverage Dashboard server running on http://localhost:${PORT}`);
});
