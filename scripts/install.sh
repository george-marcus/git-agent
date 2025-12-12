#!/bin/bash
# install.sh - Linux/macOS installation script for git-agent
# Run as: ./scripts/install.sh

set -e

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Default install directory
INSTALL_DIR="${HOME}/.local/bin"

# Detect OS and architecture
detect_runtime() {
    local os=$(uname -s | tr '[:upper:]' '[:lower:]')
    local arch=$(uname -m)

    case "$os" in
        linux)
            case "$arch" in
                x86_64) echo "linux-x64" ;;
                aarch64|arm64) echo "linux-arm64" ;;
                *) echo "linux-x64" ;;
            esac
            ;;
        darwin)
            case "$arch" in
                x86_64) echo "osx-x64" ;;
                arm64) echo "osx-arm64" ;;
                *) echo "osx-x64" ;;
            esac
            ;;
        *)
            echo "linux-x64"
            ;;
    esac
}

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --install-dir)
            INSTALL_DIR="$2"
            shift 2
            ;;
        --no-path)
            NO_PATH=1
            shift
            ;;
        -h|--help)
            echo "Usage: ./install.sh [options]"
            echo ""
            echo "Options:"
            echo "  --install-dir DIR   Install to specified directory (default: ~/.local/bin)"
            echo "  --no-path           Don't add to PATH"
            echo "  -h, --help          Show this help"
            exit 0
            ;;
        *)
            echo "Unknown option: $1"
            exit 1
            ;;
    esac
done

RUNTIME=$(detect_runtime)
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

echo -e "${CYAN}Installing git-agent...${NC}"
echo -e "Runtime: ${RUNTIME}"
echo -e "Install directory: ${INSTALL_DIR}"

# Create install directory
mkdir -p "$INSTALL_DIR"

# Build and publish
echo -e "${YELLOW}Building project...${NC}"
cd "$PROJECT_ROOT"

dotnet publish -c Release -r "$RUNTIME" --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o "$INSTALL_DIR"

# Make executable
chmod +x "$INSTALL_DIR/git-agent"

echo -e "${GREEN}Published to: ${INSTALL_DIR}${NC}"

# Add to PATH
add_to_path() {
    local shell_rc=""
    local shell_name=$(basename "$SHELL")

    case "$shell_name" in
        bash)
            if [[ -f "$HOME/.bashrc" ]]; then
                shell_rc="$HOME/.bashrc"
            elif [[ -f "$HOME/.bash_profile" ]]; then
                shell_rc="$HOME/.bash_profile"
            fi
            ;;
        zsh)
            shell_rc="$HOME/.zshrc"
            ;;
        fish)
            shell_rc="$HOME/.config/fish/config.fish"
            ;;
    esac

    if [[ -n "$shell_rc" ]]; then
        # Check if already in rc file
        if ! grep -q "git-agent" "$shell_rc" 2>/dev/null; then
            echo -e "${YELLOW}Adding to PATH in ${shell_rc}...${NC}"

            if [[ "$shell_name" == "fish" ]]; then
                echo "set -gx PATH \$PATH $INSTALL_DIR" >> "$shell_rc"
            else
                echo "" >> "$shell_rc"
                echo "# git-agent" >> "$shell_rc"
                echo "export PATH=\"\$PATH:$INSTALL_DIR\"" >> "$shell_rc"
            fi

            echo -e "${GREEN}Added to PATH${NC}"
            echo -e "${YELLOW}Run 'source ${shell_rc}' or restart your terminal${NC}"
        else
            echo -e "${GREEN}PATH already configured${NC}"
        fi
    fi
}

if [[ -z "$NO_PATH" ]]; then
    # Check if already in PATH
    if [[ ":$PATH:" != *":$INSTALL_DIR:"* ]]; then
        add_to_path
    else
        echo -e "${GREEN}${INSTALL_DIR} is already in PATH${NC}"
    fi
fi

# Verify installation
if [[ -x "$INSTALL_DIR/git-agent" ]]; then
    echo ""
    echo -e "${GREEN}Installation complete!${NC}"
    echo ""
    echo -e "${CYAN}Usage:${NC}"
    echo "  git-agent help"
    echo "  git-agent config set claude.apiKey YOUR_API_KEY"
    echo "  git-agent config use claude"
    echo "  git-agent run 'show status'"
    echo ""
else
    echo -e "${RED}Installation failed - executable not found${NC}"
    exit 1
fi
