param(
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

$deployRoot = $PSScriptRoot
$backendRoot = Split-Path -Parent $deployRoot
$workspaceRoot = Split-Path -Parent $backendRoot
$frontendRoot = Join-Path $workspaceRoot "QLStudy.Web.Portal"
$outputRoot = Join-Path $deployRoot "out"
$archivePath = Join-Path $outputRoot "qlstudy-source-$Version.tar.gz"

if (-not (Test-Path $frontendRoot)) {
    throw "Frontend repository was not found at: $frontendRoot"
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

Push-Location $workspaceRoot
try {
    tar.exe -czf $archivePath `
        --exclude='.git' `
        --exclude='.vs' `
        --exclude='node_modules' `
        --exclude='dist' `
        --exclude='bin' `
        --exclude='obj' `
        --exclude='Logs' `
        --exclude='Backups' `
        --exclude='deploy/out' `
        --exclude='*.log' `
        'QLStudy.Service.Api' `
        'QLStudy.Web.Portal'

    if ($LASTEXITCODE -ne 0) {
        throw "Source archive creation failed."
    }
}
finally {
    Pop-Location
}

Write-Host "Source deployment archive is ready: $archivePath"
