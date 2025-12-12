#!/bin/bash
# uninstall.sh - Linux/macOS uninstallation script for git-agent

set -e

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m'

INSTALL_DIR="${HOME}/.local/bin"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --install-dir)
            INSTALL_DIR="$2"
            shift 2
            ;;
        -h|--help)
            echo "Usage: ./uninstall.sh [options]"
            echo ""
            echo "Options:"
            echo "  --install-dir DIR   Directory where git-agent is installed"
            echo "  -h, --help          Show this help"
            exit 0
            ;;
        *)
            shift
            ;;
    esac
done

echo -e "${CYAN}Uninstalling git-agent...${NC}"

# Remove executable
if [[ -f "$INSTALL_DIR/git-agent" ]]; then
    echo -e "${YELLOW}Removing executable...${NC}"
    rm -f "$INSTALL_DIR/git-agent"
    echo -e "${GREEN}Removed ${INSTALL_DIR}/git-agent${NC}"
else
    echo -e "${YELLOW}Executable not found at ${INSTALL_DIR}/git-agent${NC}"
fi

# Remove PATH entries from shell rc files
remove_from_rc() {
    local rc_file="$1"
    if [[ -f "$rc_file" ]]; then
        if grep -q "git-agent" "$rc_file" 2>/dev/null; then
            echo -e "${YELLOW}Removing PATH entry from ${rc_file}...${NC}"
            # Remove git-agent related lines
            sed -i.bak '/git-agent/d' "$rc_file"
            rm -f "${rc_file}.bak"
            echo -e "${GREEN}Removed from ${rc_file}${NC}"
        fi
    fi
}

remove_from_rc "$HOME/.bashrc"
remove_from_rc "$HOME/.bash_profile"
remove_from_rc "$HOME/.zshrc"
remove_from_rc "$HOME/.config/fish/config.fish"

# Remove config directory (optional)
CONFIG_DIR="$HOME/.git-agent"
if [[ -d "$CONFIG_DIR" ]]; then
    echo ""
    read -p "Remove configuration directory ($CONFIG_DIR)? [y/N] " response
    if [[ "$response" =~ ^[Yy]$ ]]; then
        rm -rf "$CONFIG_DIR"
        echo -e "${GREEN}Removed configuration directory${NC}"
    fi
fi

echo ""
echo -e "${GREEN}Uninstallation complete!${NC}"
echo -e "${YELLOW}Please restart your terminal or run 'source ~/.bashrc' (or equivalent)${NC}"
