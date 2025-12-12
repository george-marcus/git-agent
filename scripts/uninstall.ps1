# uninstall.ps1 - Windows uninstallation script for git-agent
# Run as: .\scripts\uninstall.ps1

param(
    [string]$InstallDir = "$env:LOCALAPPDATA\git-agent"
)

$ErrorActionPreference = "Stop"

Write-Host "Uninstalling git-agent..." -ForegroundColor Cyan

# Remove from PATH
$CurrentPath = [Environment]::GetEnvironmentVariable("Path", "User")
if ($CurrentPath -like "*$InstallDir*") {
    Write-Host "Removing from PATH..." -ForegroundColor Yellow
    $NewPath = ($CurrentPath -split ';' | Where-Object { $_ -ne $InstallDir }) -join ';'
    [Environment]::SetEnvironmentVariable("Path", $NewPath, "User")
    Write-Host "Removed from PATH" -ForegroundColor Green
}

# Remove installation directory
if (Test-Path $InstallDir) {
    Write-Host "Removing installation directory..." -ForegroundColor Yellow
    Remove-Item -Recurse -Force $InstallDir
    Write-Host "Removed $InstallDir" -ForegroundColor Green
}

# Remove config directory (optional)
$ConfigDir = "$env:USERPROFILE\.git-agent"
if (Test-Path $ConfigDir) {
    $response = Read-Host "Remove configuration directory ($ConfigDir)? [y/N]"
    if ($response -eq 'y' -or $response -eq 'Y') {
        Remove-Item -Recurse -Force $ConfigDir
        Write-Host "Removed configuration directory" -ForegroundColor Green
    }
}

Write-Host ""
Write-Host "Uninstallation complete!" -ForegroundColor Green
Write-Host "Please restart your terminal for PATH changes to take effect" -ForegroundColor Yellow
