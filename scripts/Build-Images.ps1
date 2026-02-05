<#
.SYNOPSIS
    Builds and pushes Control API and Dashboard container images.

.DESCRIPTION
    Helper script that builds Docker images for both the Control API and Dashboard,
    then pushes them to the specified container registry.

.PARAMETER RegistryHost
    Container registry host and port. Default: localhost:5000

.PARAMETER DashboardApiUrl
    API base URL for the Dashboard build. Default: http://file-simulator.local:30500

.EXAMPLE
    .\Build-Images.ps1

.EXAMPLE
    .\Build-Images.ps1 -RegistryHost "registry.example.com" -DashboardApiUrl "https://api.example.com"
#>

param(
    [string]$RegistryHost = "localhost:5000",
    [string]$DashboardApiUrl = "http://file-simulator.local:30500"
)

$ErrorActionPreference = "Stop"

# Get script directory and project root
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectRoot = Split-Path -Parent $ScriptDir

function Write-ColorOutput {
    param(
        [string]$Message,
        [string]$Color = "White"
    )
    Write-Host $Message -ForegroundColor $Color
}

function Build-ControlApi {
    Write-ColorOutput "`n=== Building Control API Image ===" "Cyan"

    $imageName = "$RegistryHost/file-simulator-control-api:latest"

    Write-ColorOutput "Building $imageName..." "Yellow"

    docker build `
        -t $imageName `
        -f "$ProjectRoot/src/FileSimulator.ControlApi/Dockerfile" `
        "$ProjectRoot/src"

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build Control API image"
    }

    Write-ColorOutput "Successfully built Control API image" "Green"
    return $imageName
}

function Build-Dashboard {
    Write-ColorOutput "`n=== Building Dashboard Image ===" "Cyan"

    $imageName = "$RegistryHost/file-simulator-dashboard:latest"

    Write-ColorOutput "Building $imageName with API URL: $DashboardApiUrl..." "Yellow"

    docker build `
        -t $imageName `
        --build-arg VITE_API_BASE_URL=$DashboardApiUrl `
        "$ProjectRoot/src/FileSimulator.Dashboard"

    if ($LASTEXITCODE -ne 0) {
        throw "Failed to build Dashboard image"
    }

    Write-ColorOutput "Successfully built Dashboard image" "Green"
    return $imageName
}

function Push-Images {
    param(
        [string[]]$ImageNames
    )

    Write-ColorOutput "`n=== Pushing Images to Registry ===" "Cyan"

    foreach ($imageName in $ImageNames) {
        Write-ColorOutput "Pushing $imageName..." "Yellow"

        docker push $imageName

        if ($LASTEXITCODE -ne 0) {
            throw "Failed to push image: $imageName"
        }

        Write-ColorOutput "Successfully pushed $imageName" "Green"
    }
}

# Main execution
try {
    Write-ColorOutput "=== File Simulator Image Builder ===" "Magenta"
    Write-ColorOutput "Registry: $RegistryHost" "White"
    Write-ColorOutput "Dashboard API URL: $DashboardApiUrl" "White"

    # Build images
    $controlApiImage = Build-ControlApi
    $dashboardImage = Build-Dashboard

    # Push images
    Push-Images -ImageNames @($controlApiImage, $dashboardImage)

    Write-ColorOutput "`n=== Build Complete ===" "Green"
    Write-ColorOutput "Images:" "White"
    Write-ColorOutput "  - $controlApiImage" "White"
    Write-ColorOutput "  - $dashboardImage" "White"

    exit 0
}
catch {
    Write-ColorOutput "`nERROR: $_" "Red"
    exit 1
}
