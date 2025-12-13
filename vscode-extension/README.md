# Git Agent VS Code Extension

AI-powered natural language to git commands, integrated directly into VS Code.

## Installation

### From VSIX (Recommended)

1. Build the extension:
   ```bash
   cd vscode-extension
   npm install
   npm run compile
   npx vsce package
   ```

2. Install in VS Code:
   - Open VS Code
   - Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac)
   - Type "Extensions: Install from VSIX..."
   - Select the generated `.vsix` file

### From Source (Development)

1. Clone and build:
   ```bash
   cd vscode-extension
   npm install
   npm run compile
   ```

2. Launch in development mode:
   - Open the `vscode-extension` folder in VS Code
   - Press `F5` to launch Extension Development Host

## Prerequisites

The `git-agent` CLI must be installed and available in your PATH:

```bash
# Install as .NET global tool
dotnet tool install -g GitAgent

# Or build from source
cd /path/to/git-agent
dotnet pack
dotnet tool install -g --add-source ./nupkg GitAgent
```

## Usage

### Command Palette

Press `Ctrl+Shift+P` (or `Cmd+Shift+P` on Mac) and type:

| Command | Description |
|---------|-------------|
| `Git Agent: Run Instruction` | Enter a natural language instruction |
| `Git Agent: Run & Execute Instruction` | Run and immediately execute |
| `Git Agent: Analyze Conflicts` | Analyze merge conflicts |
| `Git Agent: Suggest Conflict Resolutions` | Get AI-suggested resolutions |
| `Git Agent: Set Provider` | Switch AI provider |
| `Git Agent: Show Status` | Show git status |

### Keyboard Shortcuts

| Shortcut | Command |
|----------|---------|
| `Ctrl+Shift+G Ctrl+Shift+R` | Run Instruction |
| `Ctrl+Shift+G Ctrl+Shift+E` | Run & Execute |

### Status Bar

Click the "Git Agent" item in the status bar to quickly run an instruction.

## Configuration

Open Settings (`Ctrl+,`) and search for "Git Agent":

| Setting | Description | Default |
|---------|-------------|---------|
| `git-agent.provider` | AI provider (claude, openai, ollama, stub) | `claude` |
| `git-agent.executablePath` | Path to git-agent executable | `git-agent` |
| `git-agent.showOutputPanel` | Show output when running commands | `true` |
| `git-agent.confirmBeforeExecute` | Confirm before executing commands | `true` |

## Examples

1. **Commit changes:**
   - `Ctrl+Shift+P` → "Git Agent: Run Instruction"
   - Type: "commit all changes with message describing what changed"

2. **Create a branch:**
   - Type: "create a new feature branch called user-auth"

3. **Push changes:**
   - Type: "push to origin"

4. **Resolve conflicts:**
   - `Ctrl+Shift+P` → "Git Agent: Analyze Conflicts"
   - Review the analysis in the output panel
   - Use "Git Agent: Suggest Conflict Resolutions" for AI help

## Troubleshooting

### "git-agent not found"

Ensure git-agent is in your PATH:
```bash
git-agent --version
```

If not found, specify the full path in settings:
```json
{
  "git-agent.executablePath": "/path/to/git-agent"
}
```

### Commands not generating

1. Check your AI provider is configured:
   ```bash
   git-agent config show
   ```

2. Set your API key:
   ```bash
   git-agent config set claude.apiKey YOUR_KEY
   ```

### Extension not loading

Check the Output panel (`View` → `Output` → select "Git Agent") for errors.
