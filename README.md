# git-agent

A .NET CLI tool that translates natural language instructions into git commands using AI providers (Claude, OpenAI, Ollama).

## Installation

### Download Pre-built Binaries

Download the latest release for your platform from [GitHub Releases](https://github.com/george-marcus/git-agent/releases):

| Platform | Download |
|----------|----------|
| Windows (x64) | `git-agent-win-x64.exe` |
| Linux (x64) | `git-agent-linux-x64` |
| macOS (Intel) | `git-agent-osx-x64` |
| macOS (Apple Silicon) | `git-agent-osx-arm64` |

#### Quick Install - Windows (PowerShell)

```powershell
New-Item -ItemType Directory -Force -Path "$env:LOCALAPPDATA\git-agent" | Out-Null
Invoke-WebRequest -Uri "https://github.com/george-marcus/git-agent/releases/latest/download/git-agent-win-x64.exe" -OutFile "$env:LOCALAPPDATA\git-agent\git-agent.exe"

# Add to PATH (run once, then restart terminal)
$path = [Environment]::GetEnvironmentVariable("Path", "User")
if ($path -notlike "*git-agent*") {
    [Environment]::SetEnvironmentVariable("Path", "$path;$env:LOCALAPPDATA\git-agent", "User")
}
```

#### Quick Install - Linux

```bash
mkdir -p ~/.local/bin
curl -L https://github.com/george-marcus/git-agent/releases/latest/download/git-agent-linux-x64 -o ~/.local/bin/git-agent
chmod +x ~/.local/bin/git-agent
```

#### Quick Install - macOS

```bash
mkdir -p ~/.local/bin

# Apple Silicon (M1/M2/M3)
curl -L https://github.com/george-marcus/git-agent/releases/latest/download/git-agent-osx-arm64 -o ~/.local/bin/git-agent

# Intel Mac
curl -L https://github.com/george-marcus/git-agent/releases/latest/download/git-agent-osx-x64 -o ~/.local/bin/git-agent

chmod +x ~/.local/bin/git-agent
```

Make sure `~/.local/bin` is in your PATH. Add to your shell profile if needed:
```bash
echo 'export PATH="$HOME/.local/bin:$PATH"' >> ~/.zshrc  # or ~/.bashrc
```

---

### Build from Source

#### Windows (PowerShell)

```powershell
git clone https://github.com/george-marcus/git-agent.git
cd git-agent
.\scripts\install.ps1
```

This will:
- Build the project as a single executable
- Install to `%LOCALAPPDATA%\git-agent`
- Add to your user PATH

#### Linux / macOS

```bash
git clone https://github.com/george-marcus/git-agent.git
cd git-agent
chmod +x ./scripts/install.sh
./scripts/install.sh
```

This will:
- Build for your platform (linux-x64, linux-arm64, osx-x64, osx-arm64)
- Install to `~/.local/bin`
- Add to your PATH via shell rc file

#### Manual Build

```bash
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true
dotnet publish -c Release -r osx-arm64 --self-contained -p:PublishSingleFile=true
```

#### Install as .NET Global Tool

```bash
dotnet pack -c Release
dotnet tool install --global --add-source ./bin/Release GitAgent
```

### Uninstall

```powershell
.\scripts\uninstall.ps1
```

```bash
./scripts/uninstall.sh
```

## Quick Start

```bash
git-agent config set claude.apiKey sk-ant-your-key-here
git-agent config use claude

git-agent run "commit all my changes with a descriptive message"

git-agent run "push to origin" --exec

git-agent run "merge feature branch into main" --exec --interactive
```

## Commands

### `run <instruction>`

Translate a natural language instruction into git commands.

```bash
git-agent run <instruction> [options]
```

**Arguments:**
- `<instruction>` - Natural language instruction to translate

**Options:**
| Option | Short | Description |
|--------|-------|-------------|
| `--exec` | `-x` | Execute the generated commands |
| `--interactive` | `-i` | Confirm each command before execution |
| `--provider <name>` | `-p` | Override the active provider for this run |
| `--no-cache` | | Skip cache and force a fresh API call |

**Examples:**
```bash
git-agent run "stage all modified files and commit with message 'fix: resolve login bug'"

git-agent run "create a new branch called feature/auth" -x

git-agent run "rebase onto main" -xi

git-agent run "show recent commits" -p openai
```

---

### `config`

Manage git-agent configuration.

#### `config show`

Display current configuration.

```bash
git-agent config show
```

#### `config set <key> <value>`

Set a configuration value.

```bash
git-agent config set <key> <value>
```

**Available keys:**
| Key | Description |
|-----|-------------|
| `activeProvider` | Active provider (claude, openai, ollama, stub) |
| `claude.apiKey` | Claude API key |
| `claude.model` | Claude model name (default: claude-sonnet-4-20250514) |
| `claude.baseUrl` | Claude API base URL |
| `openai.apiKey` | OpenAI API key |
| `openai.model` | OpenAI model name (default: gpt-4o) |
| `openai.baseUrl` | OpenAI API base URL |
| `ollama.model` | Ollama model name (default: llama3.2) |
| `ollama.baseUrl` | Ollama API base URL (default: http://localhost:11434) |

**Examples:**
```bash
git-agent config set claude.apiKey sk-ant-xxxxx
git-agent config set openai.model gpt-4-turbo
git-agent config set ollama.baseUrl http://192.168.1.100:11434
```

#### `config get <key>`

Get a configuration value.

```bash
git-agent config get claude.model
```

#### `config use <provider>`

Set the active provider.

```bash
git-agent config use <provider>
```

**Available providers:** `claude`, `openai`, `ollama`, `stub`

```bash
git-agent config use claude
git-agent config use openai
git-agent config use ollama
```

#### `config path`

Show the configuration file path.

```bash
git-agent config path
```

#### `config reset`

Reset configuration to defaults.

```bash
git-agent config reset
```

---

### `providers`

List available AI providers.

```bash
git-agent providers
```

**Output:**
```
Available providers:
  - claude (active)
  - openai
  - ollama
  - stub

Use 'git-agent config use <provider>' to switch providers.
```

---

### `conflicts`

Analyze and resolve merge conflicts with AI assistance.

```bash
git-agent conflicts [options] [file]
```

**Options:**
| Option | Short | Description |
|--------|-------|-------------|
| `--suggest` | `-s` | Show AI-suggested resolutions |
| `--resolve` | `-r` | Interactively resolve conflicts |

**Examples:**
```bash
# Analyze all conflicts
git-agent conflicts

# Get AI suggestions for resolution
git-agent conflicts -s

# Interactively resolve conflicts
git-agent conflicts -r

# Analyze specific file
git-agent conflicts src/app.ts
```

---

### `completions`

Generate shell completion scripts.

```bash
git-agent completions <shell>
```

**Supported shells:** `bash`, `zsh`, `powershell`, `fish`

**Installation:**

```bash
# Bash
git-agent completions bash >> ~/.bashrc

# Zsh
git-agent completions zsh >> ~/.zshrc

# PowerShell
git-agent completions powershell >> $PROFILE

# Fish
git-agent completions fish > ~/.config/fish/completions/git-agent.fish
```

---

### `serve`

Start JSON-RPC server for IDE integration.

```bash
git-agent serve [options]
```

**Options:**
| Option | Short | Description |
|--------|-------|-------------|
| `--port` | `-p` | Port to listen on (default: 9123) |

**Example:**
```bash
git-agent serve --port 9123
```

---

### `cache`

Manage HTTP response cache.

#### `cache clear`

Clear all cached HTTP responses.

```bash
git-agent cache clear
```

#### `cache path`

Show cache directory path.

```bash
git-agent cache path
```

---

## IDE Integration

### VS Code Extension

A full-featured VS Code extension is included in the `vscode-extension/` directory.

#### Installation

1. **Build the extension:**
   ```bash
   cd vscode-extension
   npm install
   npm run compile
   npx vsce package
   ```

2. **Install in VS Code:**
   - Press `Ctrl+Shift+P` → "Extensions: Install from VSIX..."
   - Select the generated `.vsix` file

#### Usage

| Command | Keyboard Shortcut | Description |
|---------|-------------------|-------------|
| Git Agent: Run Instruction | `Ctrl+Shift+G Ctrl+Shift+R` | Enter natural language instruction |
| Git Agent: Run & Execute | `Ctrl+Shift+G Ctrl+Shift+E` | Run and execute immediately |
| Git Agent: Analyze Conflicts | - | Analyze merge conflicts |
| Git Agent: Set Provider | - | Switch AI provider |

#### Settings

Configure in VS Code settings (`Ctrl+,`):

```json
{
  "git-agent.provider": "claude",
  "git-agent.executablePath": "git-agent",
  "git-agent.showOutputPanel": true,
  "git-agent.confirmBeforeExecute": true
}
```

See [vscode-extension/README.md](vscode-extension/README.md) for full documentation.

---

### Visual Studio (Windows)

Git-agent can be integrated with Visual Studio using External Tools:

#### Setup External Tool

1. Open Visual Studio
2. Go to **Tools** → **External Tools...**
3. Click **Add** and configure:

| Field | Value |
|-------|-------|
| Title | `Git Agent: Run` |
| Command | `cmd.exe` |
| Arguments | `/c git-agent run "$(PromptVariable)" & pause` |
| Initial directory | `$(SolutionDir)` |

4. Add another for quick execution:

| Field | Value |
|-------|-------|
| Title | `Git Agent: Run & Execute` |
| Command | `cmd.exe` |
| Arguments | `/c git-agent run "$(PromptVariable)" --exec & pause` |
| Initial directory | `$(SolutionDir)` |

#### Assign Keyboard Shortcuts

1. Go to **Tools** → **Options** → **Environment** → **Keyboard**
2. Search for `Tools.ExternalCommand1` (or the number of your git-agent tool)
3. Assign a shortcut like `Ctrl+Shift+G, Ctrl+Shift+R`

#### Using Terminal

Alternatively, use the integrated terminal:
- Open **View** → **Terminal** (`Ctrl+``)
- Run git-agent commands directly

---

### JetBrains IDEs (IntelliJ, Rider, WebStorm, etc.)

#### External Tools Setup

1. Go to **File** → **Settings** → **Tools** → **External Tools**
2. Click **+** to add a new tool:

| Field | Value |
|-------|-------|
| Name | `Git Agent Run` |
| Program | `git-agent` |
| Arguments | `run "$Prompt$"` |
| Working directory | `$ProjectFileDir$` |

3. Add another for execution:

| Field | Value |
|-------|-------|
| Name | `Git Agent Execute` |
| Program | `git-agent` |
| Arguments | `run "$Prompt$" --exec` |
| Working directory | `$ProjectFileDir$` |

#### Assign Keyboard Shortcuts

1. Go to **File** → **Settings** → **Keymap**
2. Search for "External Tools"
3. Right-click on your git-agent tool → **Add Keyboard Shortcut**
4. Assign `Ctrl+Shift+G` or your preferred shortcut

#### Using Terminal

- Open **View** → **Tool Windows** → **Terminal**
- Run git-agent commands directly

---

### JSON-RPC Server Integration

For custom IDE integrations, git-agent provides a JSON-RPC server:

```bash
git-agent serve --port 9123
```

#### Protocol

Send JSON-RPC 2.0 requests over TCP:

```json
{"jsonrpc": "2.0", "method": "git-agent/run", "params": {"instruction": "commit all changes"}, "id": 1}
```

#### Available Methods

| Method | Parameters | Description |
|--------|------------|-------------|
| `git-agent/run` | `instruction`, `provider?` | Generate git commands |
| `git-agent/conflicts` | - | Get conflict analysis |
| `git-agent/suggest` | - | Get resolution suggestions |
| `git-agent/status` | - | Get repository status |
| `git-agent/providers` | - | List available providers |

#### Example Response

```json
{
  "jsonrpc": "2.0",
  "result": {
    "commands": [
      {"commandText": "git add .", "risk": "safe"},
      {"commandText": "git commit -m \"Update files\"", "risk": "safe"}
    ],
    "context": {
      "branch": "main",
      "hasUncommittedChanges": true,
      "mergeState": "None"
    }
  },
  "id": 1
}
```

---

## Configuration

Configuration is stored in `~/.git-agent/config.json`:

```json
{
  "activeProvider": "claude",
  "providers": {
    "claude": {
      "apiKey": "sk-ant-...",
      "model": "claude-sonnet-4-5",
      "baseUrl": "https://api.anthropic.com"
    },
    "openai": {
      "apiKey": "sk-...",
      "model": "gpt-4o",
      "baseUrl": "https://api.openai.com"
    },
    "ollama": {
      "model": "llama3.2",
      "baseUrl": "http://localhost:11434"
    }
  }
}
```

## Using Ollama for Local LLM

[Ollama](https://ollama.ai) allows you to run LLMs locally without any API keys or cloud dependencies.

### 1. Install Ollama

**Windows:**
Download and run the installer from [ollama.ai/download](https://ollama.ai/download)

**Linux:**
```bash
curl -fsSL https://ollama.ai/install.sh | sh
```

**macOS:**
```bash
brew install ollama
```

### 2. Pull a Model

```bash
ollama pull llama3.2
```

Other recommended models for git commands:
- `llama3.2` (default, 3B parameters, fast)
- `llama3.1` (8B parameters, better quality)
- `codellama` (optimized for code tasks)
- `mistral` (7B parameters, good balance)

### 3. Start Ollama Server

Ollama runs as a background service. On most systems it starts automatically after installation. If not:

```bash
ollama serve
```

The server runs on `http://localhost:11434` by default.

### 4. Configure git-agent

```bash
git-agent config use ollama
git-agent config set ollama.model llama3.2
```

**Optional:** If Ollama runs on a different host or port:
```bash
git-agent config set ollama.baseUrl http://192.168.1.100:11434
```

### 5. Use git-agent

```bash
git-agent run "show me the last 5 commits"
git-agent run "commit all changes with a good message" -x
```

### Troubleshooting

**"Connection refused" error:**
- Ensure Ollama is running: `ollama serve`
- Check the URL: `curl http://localhost:11434/api/tags`

**Slow responses:**
- Use a smaller model like `llama3.2` (3B) instead of larger ones
- Ensure you have enough RAM (8GB+ recommended for 7B models)

**Poor quality responses:**
- Try a larger model: `ollama pull llama3.1`
- Some models work better for code tasks: `ollama pull codellama`

## Safety Features

Commands are validated against an allowlist and categorized by risk level:

- **Safe** (green): `status`, `add`, `commit`, `push`, `pull`, `branch`, `checkout`, `switch`, `merge`, `fetch`, `log`, `diff`, `stash`, `tag`, `remote`, `show`, `rebase`, `reset --soft`
- **Destructive** (red): Commands with `--force`, `reset --hard`, `clean`, `push --delete`, `branch -D`
- **Unknown** (yellow): Git commands not in the allowlist

Destructive commands require explicit confirmation before execution.

## Prompt Caching

The tool uses API-level prompt caching to reduce costs:

- **Claude**: Uses Anthropic's prompt caching beta (`anthropic-beta: prompt-caching-2024-07-31`)
- **OpenAI**: Automatic prompt caching for supported models

Cache status is displayed when available:
```
(prompt cache hit: 150 tokens from cache)
(prompt cache created: 200 tokens cached)
```

## Project Structure

```
├── Program.cs                      # CLI entry point with DI setup
├── Commands/
│   └── CommandBuilderExtensions.cs # CLI command definitions
├── Configuration/
│   ├── ConfigManager.cs            # Configuration loading/saving
│   ├── GitAgentConfig.cs           # Root config model
│   ├── ProviderConfigs.cs          # Provider config container
│   ├── ClaudeConfig.cs             # Claude provider settings
│   ├── OpenAIConfig.cs             # OpenAI provider settings
│   └── OllamaConfig.cs             # Ollama provider settings
├── Models/
│   ├── GeneratedCommand.cs         # Generated command with risk level
│   ├── GitTools.cs                 # Tool schema for LLM function calling
│   └── RepoContext.cs              # Git repository context + merge state
├── Providers/
│   ├── IModelProvider.cs           # Provider interface
│   ├── ProviderFactory.cs          # Provider factory with DI
│   ├── ClaudeProvider.cs           # Anthropic Claude implementation
│   ├── OpenAIProvider.cs           # OpenAI implementation
│   ├── OllamaProvider.cs           # Ollama local LLM implementation
│   └── StubProvider.cs             # Testing stub provider
├── Services/
│   ├── CachingHttpHandler.cs       # HTTP response caching
│   ├── CommandExecutor.cs          # Git command execution
│   ├── CompletionGenerator.cs      # Shell completion scripts
│   ├── ConflictResolver.cs         # Merge conflict analysis & resolution
│   ├── GitInspector.cs             # Git repository inspection
│   ├── JsonRpcServer.cs            # JSON-RPC server for IDEs
│   ├── PromptBuilder.cs            # LLM prompt construction
│   ├── ResponseParser.cs           # Text response parsing (Ollama fallback)
│   └── SafetyValidator.cs          # Command risk validation
├── vscode-extension/               # VS Code extension
│   ├── src/extension.ts            # Extension entry point
│   ├── package.json                # Extension manifest
│   └── README.md                   # Extension documentation
├── scripts/                        # Installation scripts
└── tests/                          # xUnit test suite
```

## License

MIT
