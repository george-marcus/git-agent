namespace GitAgent.Services;

public interface ICompletionGenerator
{
    string GenerateBashCompletion();
    string GenerateZshCompletion();
    string GeneratePowerShellCompletion();
    string GenerateFishCompletion();
}

public class CompletionGenerator : ICompletionGenerator
{
    public string GenerateBashCompletion()
    {
        return """
            # git-agent bash completion
            # Add this to ~/.bashrc or ~/.bash_completion

            _git_agent_completions()
            {
                local cur prev opts commands
                COMPREPLY=()
                cur="${COMP_WORDS[COMP_CWORD]}"
                prev="${COMP_WORDS[COMP_CWORD-1]}"

                # Top-level commands
                commands="run config providers cache conflicts completions help"

                # Config subcommands
                config_commands="show set get use path reset"

                # Cache subcommands
                cache_commands="clear path"
           
                # Completions subcommands
                completions_shells="bash zsh powershell fish"

                case "${prev}" in
                    git-agent)
                        COMPREPLY=( $(compgen -W "${commands}" -- ${cur}) )
                        return 0
                        ;;
                    run)
                        COMPREPLY=( $(compgen -W "--exec -x --interactive -i --provider -p --no-cache" -- ${cur}) )
                        return 0
                        ;;
                    config)
                        COMPREPLY=( $(compgen -W "${config_commands}" -- ${cur}) )
                        return 0
                        ;;
                    cache)
                        COMPREPLY=( $(compgen -W "${cache_commands}" -- ${cur}) )
                        return 0
                        ;;
                    conflicts)
                        COMPREPLY=( $(compgen -W "--suggest -s --resolve -r --apply -a --provider -p" -- ${cur}) )
                        return 0
                        ;;
                    completions)
                        COMPREPLY=( $(compgen -W "${completions_shells}" -- ${cur}) )
                        return 0
                        ;;
                    use|--provider|-p)
                        COMPREPLY=( $(compgen -W "claude openai openrouter ollama stub" -- ${cur}) )
                        return 0
                        ;;
                    set)
                        COMPREPLY=( $(compgen -W "activeProvider claude.apiKey claude.model claude.baseUrl openai.apiKey openai.model openai.baseUrl openrouter.apiKey openrouter.model openrouter.baseUrl openrouter.siteName openrouter.siteUrl ollama.model ollama.baseUrl" -- ${cur}) )
                        return 0
                        ;;
                    get)
                        COMPREPLY=( $(compgen -W "activeProvider claude.apiKey claude.model claude.baseUrl openai.apiKey openai.model openai.baseUrl openrouter.apiKey openrouter.model openrouter.baseUrl openrouter.siteName openrouter.siteUrl ollama.model ollama.baseUrl" -- ${cur}) )
                        return 0
                        ;;
                    *)
                        ;;
                esac
            }

            complete -F _git_agent_completions git-agent
            """;
    }

    public string GenerateZshCompletion()
    {
        return """
            #compdef git-agent
            # git-agent zsh completion
            # Add this to ~/.zshrc or place in a file in your $fpath

            _git-agent() {
                local -a commands
                local -a config_commands
                local -a cache_commands
                local -a providers

                commands=(
                    'run:Translate and execute a plain English instruction'
                    'config:Manage git-agent configuration'
                    'providers:List available AI providers'
                    'cache:Manage HTTP response cache'
                    'conflicts:Analyze and resolve merge conflicts'
                    'completions:Generate shell completions'
                    'help:Show help and list all available commands'
                )

                config_commands=(
                    'show:Display current configuration'
                    'set:Set a configuration value'
                    'get:Get a configuration value'
                    'use:Set the active provider'
                    'path:Show the configuration file path'
                    'reset:Reset configuration to defaults'
                )

                cache_commands=(
                    'clear:Clear all cached HTTP responses'
                    'path:Show cache directory path'
                )
                providers=(claude openai openrouter ollama stub)

                _arguments -C \
                    '1: :->command' \
                    '*: :->args'

                case $state in
                    command)
                        _describe 'command' commands
                        ;;
                    args)
                        case $words[2] in
                            run)
                                _arguments \
                                    '(-x --exec)'{-x,--exec}'[Execute the resulting commands]' \
                                    '(-i --interactive)'{-i,--interactive}'[Confirm each step interactively]' \
                                    '(-p --provider)'{-p,--provider}'[Override the active provider]:provider:($providers)' \
                                    '--no-cache[Skip cache and force a fresh API call]' \
                                    '*:instruction:'
                                ;;
                            config)
                                _describe 'config command' config_commands
                                ;;
                            cache)
                                _describe 'cache command' cache_commands
                                ;;
                            conflicts)
                                _arguments \
                                    '(-s --suggest)'{-s,--suggest}'[Show AI-suggested resolutions]' \
                                    '(-r --resolve)'{-r,--resolve}'[Interactively resolve conflicts]' \
                                    '(-a --apply)'{-a,--apply}'[Auto-apply AI-suggested resolutions]' \
                                    '(-p --provider)'{-p,--provider}'[Override the active provider]:provider:($providers)' \
                                    '*:file:_files'
                                ;;
                            completions)
                                _arguments '1:shell:(bash zsh powershell fish)'
                                ;;
                        esac
                        ;;
                esac
            }

            compdef _git-agent git-agent
            """;
    }

    public string GeneratePowerShellCompletion()
    {
        return """
            # git-agent PowerShell completion
            # Add this to your PowerShell profile ($PROFILE)

            Register-ArgumentCompleter -Native -CommandName git-agent -ScriptBlock {
                param($wordToComplete, $commandAst, $cursorPosition)

                $commands = @{
                    'run' = @('--exec', '-x', '--interactive', '-i', '--provider', '-p', '--no-cache')
                    'config' = @('show', 'set', 'get', 'use', 'path', 'reset')
                    'cache' = @('clear', 'path')
                    'conflicts' = @('--suggest', '-s', '--resolve', '-r', '--apply', '-a', '--provider', '-p')
                    'completions' = @('bash', 'zsh', 'powershell', 'fish')
                }

                $providers = @('claude', 'openai', 'openrouter', 'ollama', 'stub')
                $configKeys = @('activeProvider', 'claude.apiKey', 'claude.model', 'claude.baseUrl',
                               'openai.apiKey', 'openai.model', 'openai.baseUrl',
                               'openrouter.apiKey', 'openrouter.model', 'openrouter.baseUrl', 'openrouter.siteName', 'openrouter.siteUrl',
                               'ollama.model', 'ollama.baseUrl')

                $elements = $commandAst.CommandElements
                $command = ''
                $subcommand = ''

                if ($elements.Count -ge 2) {
                    $command = $elements[1].Extent.Text
                }
                if ($elements.Count -ge 3) {
                    $subcommand = $elements[2].Extent.Text
                }

                $completions = @()

                if ($elements.Count -eq 1 -or ($elements.Count -eq 2 -and $wordToComplete)) {
                    # Complete top-level commands
                    $completions = @('run', 'config', 'providers', 'cache', 'conflicts', 'completions', 'help')
                }
                elseif ($commands.ContainsKey($command)) {
                    $completions = $commands[$command]

                    # Special handling for provider selection
                    if ($command -eq 'run' -and $subcommand -in @('--provider', '-p')) {
                        $completions = $providers
                    }
                    elseif ($command -eq 'conflicts' -and $subcommand -in @('--provider', '-p')) {
                        $completions = $providers
                    }
                    elseif ($command -eq 'config' -and $subcommand -eq 'use') {
                        $completions = $providers
                    }
                    elseif ($command -eq 'config' -and $subcommand -in @('set', 'get')) {
                        $completions = $configKeys
                    }
                }

                $completions | Where-Object { $_ -like "$wordToComplete*" } | ForEach-Object {
                    [System.Management.Automation.CompletionResult]::new($_, $_, 'ParameterValue', $_)
                }
            }
            """;
    }

    public string GenerateFishCompletion()
    {
        return """
            # git-agent fish completion
            # Save to ~/.config/fish/completions/git-agent.fish

            # Disable file completion by default
            complete -c git-agent -f

            # Top-level commands
            complete -c git-agent -n "__fish_use_subcommand" -a "run" -d "Translate and execute a plain English instruction"
            complete -c git-agent -n "__fish_use_subcommand" -a "config" -d "Manage git-agent configuration"
            complete -c git-agent -n "__fish_use_subcommand" -a "providers" -d "List available AI providers"
            complete -c git-agent -n "__fish_use_subcommand" -a "cache" -d "Manage HTTP response cache"
            complete -c git-agent -n "__fish_use_subcommand" -a "conflicts" -d "Analyze and resolve merge conflicts"
            complete -c git-agent -n "__fish_use_subcommand" -a "completions" -d "Generate shell completions"
            complete -c git-agent -n "__fish_use_subcommand" -a "help" -d "Show help"

            # run command options
            complete -c git-agent -n "__fish_seen_subcommand_from run" -s x -l exec -d "Execute the resulting commands"
            complete -c git-agent -n "__fish_seen_subcommand_from run" -s i -l interactive -d "Confirm each step"
            complete -c git-agent -n "__fish_seen_subcommand_from run" -s p -l provider -d "Override the active provider" -xa "claude openai openrouter ollama stub"
            complete -c git-agent -n "__fish_seen_subcommand_from run" -l no-cache -d "Skip cache"

            # config subcommands
            complete -c git-agent -n "__fish_seen_subcommand_from config; and not __fish_seen_subcommand_from show set get use path reset" -a "show" -d "Display current configuration"
            complete -c git-agent -n "__fish_seen_subcommand_from config; and not __fish_seen_subcommand_from show set get use path reset" -a "set" -d "Set a configuration value"
            complete -c git-agent -n "__fish_seen_subcommand_from config; and not __fish_seen_subcommand_from show set get use path reset" -a "get" -d "Get a configuration value"
            complete -c git-agent -n "__fish_seen_subcommand_from config; and not __fish_seen_subcommand_from show set get use path reset" -a "use" -d "Set the active provider"
            complete -c git-agent -n "__fish_seen_subcommand_from config; and not __fish_seen_subcommand_from show set get use path reset" -a "path" -d "Show configuration file path"
            complete -c git-agent -n "__fish_seen_subcommand_from config; and not __fish_seen_subcommand_from show set get use path reset" -a "reset" -d "Reset to defaults"

            # config use providers
            complete -c git-agent -n "__fish_seen_subcommand_from use" -a "claude openai openrouter ollama stub" -d "Provider"

            # config set/get keys
            complete -c git-agent -n "__fish_seen_subcommand_from set get" -a "activeProvider claude.apiKey claude.model claude.baseUrl openai.apiKey openai.model openai.baseUrl openrouter.apiKey openrouter.model openrouter.baseUrl openrouter.siteName openrouter.siteUrl ollama.model ollama.baseUrl"

            # cache subcommands
            complete -c git-agent -n "__fish_seen_subcommand_from cache; and not __fish_seen_subcommand_from clear path" -a "clear" -d "Clear all cached responses"
            complete -c git-agent -n "__fish_seen_subcommand_from cache; and not __fish_seen_subcommand_from clear path" -a "path" -d "Show cache directory"

            # conflicts options
            complete -c git-agent -n "__fish_seen_subcommand_from conflicts" -s s -l suggest -d "Show AI-suggested resolutions"
            complete -c git-agent -n "__fish_seen_subcommand_from conflicts" -s r -l resolve -d "Interactively resolve conflicts"
            complete -c git-agent -n "__fish_seen_subcommand_from conflicts" -s a -l apply -d "Auto-apply AI-suggested resolutions"
            complete -c git-agent -n "__fish_seen_subcommand_from conflicts" -s p -l provider -d "Override the active provider" -xa "claude openai openrouter ollama stub"

            # completions shells
            complete -c git-agent -n "__fish_seen_subcommand_from completions" -a "bash zsh powershell fish" -d "Shell type"
            """;
    }
}
