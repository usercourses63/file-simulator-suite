# File Simulator Suite - Project Initialization Script
# Run this FIRST before using Claude Code

param(
    [string]$ProjectPath = "C:\Projects\file-simulator-suite",
    [switch]$SkipNuGet = $false
)

$ErrorActionPreference = "Stop"

Write-Host "=============================================" -ForegroundColor Cyan
Write-Host "  File Simulator Suite - Project Setup" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan

# 1. Check prerequisites
Write-Host "`n[1/7] Checking prerequisites..." -ForegroundColor Yellow

$missing = @()

if (-not (Get-Command node -ErrorAction SilentlyContinue)) { $missing += "Node.js" }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { $missing += ".NET SDK" }
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { $missing += "Docker" }
if (-not (Get-Command minikube -ErrorAction SilentlyContinue)) { $missing += "Minikube" }
if (-not (Get-Command helm -ErrorAction SilentlyContinue)) { $missing += "Helm" }
if (-not (Get-Command kubectl -ErrorAction SilentlyContinue)) { $missing += "kubectl" }
if (-not (Get-Command git -ErrorAction SilentlyContinue)) { $missing += "Git" }

if ($missing.Count -gt 0) {
    Write-Host "  Missing prerequisites: $($missing -join ', ')" -ForegroundColor Red
    Write-Host "  Please install them before continuing." -ForegroundColor Red
    exit 1
}

# Check Claude Code
$claudeInstalled = npm list -g @anthropic-ai/claude-code 2>$null
if (-not $claudeInstalled -or $claudeInstalled -match "empty") {
    Write-Host "  Claude Code not found. Installing..." -ForegroundColor Yellow
    npm install -g @anthropic-ai/claude-code
}

Write-Host "  All prerequisites found!" -ForegroundColor Green

# 2. Create project directory
Write-Host "`n[2/7] Creating project directory..." -ForegroundColor Yellow

if (Test-Path $ProjectPath) {
    Write-Host "  Directory exists: $ProjectPath" -ForegroundColor Yellow
    $confirm = Read-Host "  Continue and potentially overwrite? (y/N)"
    if ($confirm -ne 'y') {
        Write-Host "  Aborted." -ForegroundColor Red
        exit 1
    }
} else {
    New-Item -ItemType Directory -Path $ProjectPath -Force | Out-Null
}

Set-Location $ProjectPath
Write-Host "  Working directory: $ProjectPath" -ForegroundColor Green

# 3. Initialize Git
Write-Host "`n[3/7] Initializing Git repository..." -ForegroundColor Yellow

if (-not (Test-Path ".git")) {
    git init | Out-Null
    Write-Host "  Git repository initialized" -ForegroundColor Green
} else {
    Write-Host "  Git repository already exists" -ForegroundColor Green
}

# 4. Create directory structure
Write-Host "`n[4/7] Creating directory structure..." -ForegroundColor Yellow

$directories = @(
    "helm-chart/file-simulator/templates",
    "helm-chart/file-simulator/files",
    "helm-chart/samples",
    "scripts",
    "src/FileSimulator.Client/Services",
    "src/FileSimulator.Client/Extensions", 
    "src/FileSimulator.Client/Examples",
    "tests/FileSimulator.Client.Tests"
)

foreach ($dir in $directories) {
    New-Item -ItemType Directory -Path $dir -Force | Out-Null
}
Write-Host "  Created $($directories.Count) directories" -ForegroundColor Green

# 5. Create .gitignore
Write-Host "`n[5/7] Creating .gitignore..." -ForegroundColor Yellow

@"
# .NET
bin/
obj/
*.user
*.suo
.vs/
*.DotSettings.user

# IDE
.idea/
.vscode/settings.json

# Build
publish/
artifacts/
TestResults/

# Secrets (keep examples)
appsettings.*.json
!appsettings.example.json
!appsettings.complete.json
!appsettings.development.json

# Helm
*.tgz
charts/

# Temp
*.tmp
*.log
*.bak

# OS
.DS_Store
Thumbs.db
"@ | Out-File -FilePath ".gitignore" -Encoding UTF8

Write-Host "  Created .gitignore" -ForegroundColor Green

# 6. Initialize .NET solution
Write-Host "`n[6/7] Initializing .NET solution..." -ForegroundColor Yellow

# Create solution
if (-not (Test-Path "FileSimulatorSuite.sln")) {
    dotnet new sln -n FileSimulatorSuite | Out-Null
    Write-Host "  Created solution file" -ForegroundColor Green
}

# Create class library
if (-not (Test-Path "src/FileSimulator.Client/FileSimulator.Client.csproj")) {
    dotnet new classlib -n FileSimulator.Client -o src/FileSimulator.Client -f net9.0 | Out-Null
    
    # Remove default Class1.cs
    Remove-Item "src/FileSimulator.Client/Class1.cs" -ErrorAction SilentlyContinue
    
    Write-Host "  Created FileSimulator.Client project" -ForegroundColor Green
}

# Create test project
if (-not (Test-Path "tests/FileSimulator.Client.Tests/FileSimulator.Client.Tests.csproj")) {
    dotnet new xunit -n FileSimulator.Client.Tests -o tests/FileSimulator.Client.Tests -f net9.0 | Out-Null
    Write-Host "  Created test project" -ForegroundColor Green
}

# Add projects to solution
dotnet sln add src/FileSimulator.Client/FileSimulator.Client.csproj 2>$null
dotnet sln add tests/FileSimulator.Client.Tests/FileSimulator.Client.Tests.csproj 2>$null

# Add project reference
dotnet add tests/FileSimulator.Client.Tests reference src/FileSimulator.Client 2>$null

Write-Host "  Solution configured" -ForegroundColor Green

# 7. Add NuGet packages
if (-not $SkipNuGet) {
    Write-Host "`n[7/7] Adding NuGet packages..." -ForegroundColor Yellow
    
    Push-Location "src/FileSimulator.Client"
    
    $packages = @(
        @{ Name = "AWSSDK.S3"; Version = "3.7.305" },
        @{ Name = "FluentFTP"; Version = "50.0.1" },
        @{ Name = "SSH.NET"; Version = "2024.1.0" },
        @{ Name = "SMBLibrary"; Version = "1.5.2" },
        @{ Name = "Microsoft.Extensions.Configuration.Abstractions"; Version = "9.0.0" },
        @{ Name = "Microsoft.Extensions.DependencyInjection.Abstractions"; Version = "9.0.0" },
        @{ Name = "Microsoft.Extensions.Options"; Version = "9.0.0" },
        @{ Name = "Microsoft.Extensions.Http.Polly"; Version = "9.0.0" },
        @{ Name = "Quartz"; Version = "3.8.1" },
        @{ Name = "Quartz.Extensions.Hosting"; Version = "3.8.1" },
        @{ Name = "Quartz.Extensions.DependencyInjection"; Version = "3.8.1" },
        @{ Name = "AspNetCore.HealthChecks.Network"; Version = "8.0.1" },
        @{ Name = "AspNetCore.HealthChecks.Uris"; Version = "8.0.1" },
        @{ Name = "MassTransit"; Version = "8.2.5" },
        @{ Name = "MassTransit.RabbitMQ"; Version = "8.2.5" }
    )
    
    foreach ($pkg in $packages) {
        Write-Host "  Adding $($pkg.Name)..." -ForegroundColor Gray
        dotnet add package $pkg.Name --version $pkg.Version 2>$null | Out-Null
    }
    
    Pop-Location
    Write-Host "  Added $($packages.Count) packages" -ForegroundColor Green
} else {
    Write-Host "`n[7/7] Skipping NuGet packages (use -SkipNuGet:$false to add)" -ForegroundColor Yellow
}

# Create CLAUDE.md placeholder
Write-Host "`nCreating CLAUDE.md..." -ForegroundColor Yellow
if (-not (Test-Path "CLAUDE.md")) {
    @"
# CLAUDE.md - File Simulator Suite

## Project Overview
This project implements a File Access Simulator Suite for Kubernetes/OpenShift.

## Quick Start
Run prompts in order from IMPLEMENTATION-GUIDE.md

## See Also
- IMPLEMENTATION-GUIDE.md - Detailed step-by-step instructions
- README.md - User documentation
"@ | Out-File -FilePath "CLAUDE.md" -Encoding UTF8
}

# Summary
Write-Host "`n=============================================" -ForegroundColor Cyan
Write-Host "  Setup Complete!" -ForegroundColor Cyan
Write-Host "=============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Project location: $ProjectPath" -ForegroundColor White
Write-Host ""
Write-Host "Directory structure:" -ForegroundColor White
Write-Host "  $ProjectPath\"
Write-Host "  ├── helm-chart/file-simulator/  <- Helm chart"
Write-Host "  ├── scripts/                    <- PowerShell scripts"
Write-Host "  ├── src/FileSimulator.Client/   <- .NET library"
Write-Host "  ├── tests/                      <- Unit tests"
Write-Host "  ├── CLAUDE.md                   <- Claude Code instructions"
Write-Host "  └── FileSimulatorSuite.sln      <- Solution file"
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "  1. Copy CLAUDE.md and IMPLEMENTATION-GUIDE.md from the zip file"
Write-Host "  2. Start Claude Code: claude"
Write-Host "  3. Follow the prompts in IMPLEMENTATION-GUIDE.md"
Write-Host ""
Write-Host "Or run directly:" -ForegroundColor Yellow
Write-Host "  cd $ProjectPath"
Write-Host "  claude"
Write-Host ""
