# install.ps1 - Windows installation script for git-agent
# Run as: .\scripts\install.ps1

param(
    [string]$InstallDir = "$env:LOCALAPPDATA\git-agent",
    [switch]$AddToPath = $true
)

$ErrorActionPreference = "Stop"

Write-Host "Installing git-agent..." -ForegroundColor Cyan

# Get script directory and project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

Write-Host "Building project..." -ForegroundColor Yellow
Push-Location $ProjectRoot
try {
    dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -o $InstallDir

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed"
    }
}
finally {
    Pop-Location
}

Write-Host "Published to: $InstallDir" -ForegroundColor Green

# Add to PATH if requested
if ($AddToPath) {
    $CurrentPath = [Environment]::GetEnvironmentVariable("Path", "User")

    if ($CurrentPath -notlike "*$InstallDir*") {
        Write-Host "Adding to PATH..." -ForegroundColor Yellow
        $NewPath = "$CurrentPath;$InstallDir"
        [Environment]::SetEnvironmentVariable("Path", $NewPath, "User")
        Write-Host "Added $InstallDir to user PATH" -ForegroundColor Green
        Write-Host "Please restart your terminal for PATH changes to take effect" -ForegroundColor Yellow
    }
    else {
        Write-Host "$InstallDir is already in PATH" -ForegroundColor Green
    }
}

# Verify installation
$ExePath = Join-Path $InstallDir "git-agent.exe"
if (Test-Path $ExePath) {
    Write-Host ""
    Write-Host "Installation complete!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Usage:" -ForegroundColor Cyan
    Write-Host "  git-agent help"
    Write-Host "  git-agent config set claude.apiKey YOUR_API_KEY"
    Write-Host "  git-agent config use claude"
    Write-Host "  git-agent run 'show status'"
    Write-Host ""
}
else {
    Write-Host "Installation failed - executable not found" -ForegroundColor Red
    exit 1
}
