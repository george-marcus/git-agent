import * as vscode from 'vscode';
import { exec } from 'child_process';
import { promisify } from 'util';

const execAsync = promisify(exec);

let outputChannel: vscode.OutputChannel;

export function activate(context: vscode.ExtensionContext) {
    outputChannel = vscode.window.createOutputChannel('Git Agent');

    // Register commands
    context.subscriptions.push(
        vscode.commands.registerCommand('git-agent.run', runInstruction),
        vscode.commands.registerCommand('git-agent.runAndExecute', runAndExecuteInstruction),
        vscode.commands.registerCommand('git-agent.conflicts', analyzeConflicts),
        vscode.commands.registerCommand('git-agent.suggestResolutions', suggestResolutions),
        vscode.commands.registerCommand('git-agent.setProvider', setProvider),
        vscode.commands.registerCommand('git-agent.showStatus', showStatus)
    );

    // Status bar item
    const statusBarItem = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
    statusBarItem.text = '$(git-branch) Git Agent';
    statusBarItem.command = 'git-agent.run';
    statusBarItem.tooltip = 'Click to run a git instruction';
    statusBarItem.show();
    context.subscriptions.push(statusBarItem);
}

async function getExecutablePath(): Promise<string> {
    const config = vscode.workspace.getConfiguration('git-agent');
    return config.get('executablePath', 'git-agent');
}

async function getWorkspaceFolder(): Promise<string | undefined> {
    const folders = vscode.workspace.workspaceFolders;
    if (!folders || folders.length === 0) {
        vscode.window.showErrorMessage('No workspace folder open');
        return undefined;
    }
    return folders[0].uri.fsPath;
}

async function runGitAgent(args: string, cwd: string): Promise<{ stdout: string; stderr: string }> {
    const executable = await getExecutablePath();
    const command = `"${executable}" ${args}`;

    outputChannel.appendLine(`> ${command}`);

    try {
        const result = await execAsync(command, { cwd, timeout: 60000 });
        return result;
    } catch (error: any) {
        if (error.stdout) {
            return { stdout: error.stdout, stderr: error.stderr || '' };
        }
        throw error;
    }
}

async function runInstruction() {
    const cwd = await getWorkspaceFolder();
    if (!cwd) return;

    const instruction = await vscode.window.showInputBox({
        prompt: 'Enter a natural language git instruction',
        placeHolder: 'e.g., "commit all changes with a descriptive message"'
    });

    if (!instruction) return;

    const config = vscode.workspace.getConfiguration('git-agent');
    const showOutput = config.get('showOutputPanel', true);

    if (showOutput) {
        outputChannel.show(true);
    }

    outputChannel.appendLine(`\n--- Running instruction: "${instruction}" ---`);

    try {
        const provider = config.get('provider', 'claude');
        const result = await runGitAgent(`run "${instruction}" --provider ${provider}`, cwd);

        outputChannel.appendLine(result.stdout);
        if (result.stderr) {
            outputChannel.appendLine(`[stderr] ${result.stderr}`);
        }

        // Parse the generated commands
        const commands = parseGeneratedCommands(result.stdout);

        if (commands.length > 0) {
            // Show quick pick to optionally execute
            const choice = await vscode.window.showQuickPick(
                [
                    { label: '$(play) Execute All', description: 'Run all generated commands', value: 'execute' },
                    { label: '$(list-selection) Select Commands', description: 'Choose which commands to run', value: 'select' },
                    { label: '$(close) Cancel', description: 'Do not execute', value: 'cancel' }
                ],
                { placeHolder: 'What would you like to do with the generated commands?' }
            );

            if (choice?.value === 'execute') {
                await executeCommands(commands, cwd);
            } else if (choice?.value === 'select') {
                await selectAndExecuteCommands(commands, cwd);
            }
        }
    } catch (error: any) {
        outputChannel.appendLine(`Error: ${error.message}`);
        vscode.window.showErrorMessage(`Git Agent error: ${error.message}`);
    }
}

async function runAndExecuteInstruction() {
    const cwd = await getWorkspaceFolder();
    if (!cwd) return;

    const instruction = await vscode.window.showInputBox({
        prompt: 'Enter a natural language git instruction (will execute immediately)',
        placeHolder: 'e.g., "push to origin"'
    });

    if (!instruction) return;

    const config = vscode.workspace.getConfiguration('git-agent');
    const confirmBeforeExecute = config.get('confirmBeforeExecute', true);

    outputChannel.show(true);
    outputChannel.appendLine(`\n--- Running and executing: "${instruction}" ---`);

    try {
        const provider = config.get('provider', 'claude');
        const result = await runGitAgent(`run "${instruction}" --provider ${provider}`, cwd);

        outputChannel.appendLine(result.stdout);

        const commands = parseGeneratedCommands(result.stdout);

        if (commands.length > 0) {
            if (confirmBeforeExecute) {
                const confirm = await vscode.window.showWarningMessage(
                    `Execute ${commands.length} command(s)?`,
                    { modal: true },
                    'Execute'
                );
                if (confirm !== 'Execute') return;
            }

            await executeCommands(commands, cwd);
        }
    } catch (error: any) {
        outputChannel.appendLine(`Error: ${error.message}`);
        vscode.window.showErrorMessage(`Git Agent error: ${error.message}`);
    }
}

async function analyzeConflicts() {
    const cwd = await getWorkspaceFolder();
    if (!cwd) return;

    outputChannel.show(true);
    outputChannel.appendLine('\n--- Analyzing conflicts ---');

    try {
        const result = await runGitAgent('conflicts', cwd);
        outputChannel.appendLine(result.stdout);

        if (result.stdout.includes('No merge in progress')) {
            vscode.window.showInformationMessage('No merge conflicts detected.');
        } else {
            vscode.window.showInformationMessage('Conflict analysis complete. See output panel for details.');
        }
    } catch (error: any) {
        outputChannel.appendLine(`Error: ${error.message}`);
        vscode.window.showErrorMessage(`Git Agent error: ${error.message}`);
    }
}

async function suggestResolutions() {
    const cwd = await getWorkspaceFolder();
    if (!cwd) return;

    outputChannel.show(true);
    outputChannel.appendLine('\n--- Suggesting conflict resolutions ---');

    try {
        const result = await runGitAgent('conflicts --suggest', cwd);
        outputChannel.appendLine(result.stdout);
        vscode.window.showInformationMessage('Resolution suggestions generated. See output panel.');
    } catch (error: any) {
        outputChannel.appendLine(`Error: ${error.message}`);
        vscode.window.showErrorMessage(`Git Agent error: ${error.message}`);
    }
}

async function setProvider() {
    const providers = ['claude', 'openai', 'ollama', 'stub'];
    const config = vscode.workspace.getConfiguration('git-agent');
    const current = config.get('provider', 'claude');

    const selected = await vscode.window.showQuickPick(
        providers.map(p => ({
            label: p === current ? `$(check) ${p}` : p,
            description: p === current ? '(current)' : '',
            value: p
        })),
        { placeHolder: 'Select AI provider' }
    );

    if (selected) {
        await config.update('provider', selected.value, vscode.ConfigurationTarget.Workspace);
        vscode.window.showInformationMessage(`Git Agent provider set to: ${selected.value}`);
    }
}

async function showStatus() {
    const cwd = await getWorkspaceFolder();
    if (!cwd) return;

    try {
        const result = await runGitAgent('run "show status" --provider stub', cwd);

        // Also get actual git status
        const gitResult = await execAsync('git status --short', { cwd });

        const panel = vscode.window.createWebviewPanel(
            'gitAgentStatus',
            'Git Agent Status',
            vscode.ViewColumn.One,
            {}
        );

        panel.webview.html = `
            <!DOCTYPE html>
            <html>
            <head>
                <style>
                    body { font-family: var(--vscode-font-family); padding: 20px; }
                    pre { background: var(--vscode-textBlockQuote-background); padding: 10px; }
                    h2 { color: var(--vscode-foreground); }
                </style>
            </head>
            <body>
                <h2>Git Status</h2>
                <pre>${gitResult.stdout || 'Working tree clean'}</pre>
            </body>
            </html>
        `;
    } catch (error: any) {
        vscode.window.showErrorMessage(`Error: ${error.message}`);
    }
}

function parseGeneratedCommands(output: string): string[] {
    const commands: string[] = [];
    const lines = output.split('\n');

    let inCommandSection = false;
    for (const line of lines) {
        if (line.includes('Generated commands:') || line.includes('---')) {
            inCommandSection = true;
            continue;
        }

        if (inCommandSection && line.trim().startsWith('git ')) {
            // Remove ANSI escape codes and extract command
            const cleanLine = line.replace(/\x1b\[[0-9;]*m/g, '').trim();
            commands.push(cleanLine);
        }
    }

    return commands;
}

async function executeCommands(commands: string[], cwd: string) {
    outputChannel.appendLine('\n--- Executing commands ---');

    for (const command of commands) {
        outputChannel.appendLine(`> ${command}`);
        try {
            const result = await execAsync(command, { cwd });
            if (result.stdout) {
                outputChannel.appendLine(result.stdout);
            }
            if (result.stderr) {
                outputChannel.appendLine(`[stderr] ${result.stderr}`);
            }
        } catch (error: any) {
            outputChannel.appendLine(`Error: ${error.message}`);
            const continueExec = await vscode.window.showErrorMessage(
                `Command failed: ${command}`,
                'Continue',
                'Stop'
            );
            if (continueExec !== 'Continue') {
                break;
            }
        }
    }

    outputChannel.appendLine('--- Execution complete ---');
    vscode.window.showInformationMessage('Git Agent: Commands executed');
}

async function selectAndExecuteCommands(commands: string[], cwd: string) {
    const items = commands.map((cmd, i) => ({
        label: cmd,
        picked: true,
        index: i
    }));

    const selected = await vscode.window.showQuickPick(items, {
        canPickMany: true,
        placeHolder: 'Select commands to execute'
    });

    if (selected && selected.length > 0) {
        const selectedCommands = selected.map(s => s.label);
        await executeCommands(selectedCommands, cwd);
    }
}

export function deactivate() {
    if (outputChannel) {
        outputChannel.dispose();
    }
}
