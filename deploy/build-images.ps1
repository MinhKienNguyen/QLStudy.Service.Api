param(
    [string]$Version = "1.0.0",
    [string]$Platform = "linux/amd64"
)

$ErrorActionPreference = "Stop"

$deployRoot = $PSScriptRoot
$backendRoot = Split-Path -Parent $deployRoot
$workspaceRoot = Split-Path -Parent $backendRoot
$frontendRoot = Join-Path $workspaceRoot "QLStudy.Web.Portal"
$outputRoot = Join-Path $deployRoot "out"
$archivePath = Join-Path $outputRoot "qlstudy-images-$Version-linux-amd64.tar"

if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw "Docker CLI was not found. Install and start Docker Desktop first."
}

if (-not (Test-Path (Join-Path $frontendRoot "Dockerfile"))) {
    throw "Frontend repository was not found at: $frontendRoot"
}

New-Item -ItemType Directory -Force -Path $outputRoot | Out-Null

Write-Host "Building API image qlstudy-api:$Version..."
docker buildx build --platform $Platform --load --tag "qlstudy-api:$Version" $backendRoot
if ($LASTEXITCODE -ne 0) { throw "API image build failed." }

Write-Host "Building web image qlstudy-web:$Version..."
docker buildx build --platform $Platform --load --tag "qlstudy-web:$Version" $frontendRoot
if ($LASTEXITCODE -ne 0) { throw "Web image build failed." }

Write-Host "Pulling PostgreSQL image for $Platform..."
docker pull --platform $Platform postgres:17-alpine
if ($LASTEXITCODE -ne 0) { throw "PostgreSQL image pull failed." }

Write-Host "Saving images to $archivePath..."
docker save --output $archivePath "qlstudy-api:$Version" "qlstudy-web:$Version" postgres:17-alpine
if ($LASTEXITCODE -ne 0) { throw "Image archive creation failed." }

Write-Host "Deployment archive is ready: $archivePath"
Write-Host "Copy the archive, compose.production.yml and .env.production to the server."
