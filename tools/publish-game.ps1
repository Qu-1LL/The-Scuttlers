[CmdletBinding()]
param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release",
    [string]$ProjectPath = "src/TriloGame.Game/TriloGame.Game.csproj",
    [string]$OutputRoot = "artifacts/publish",
    [bool]$SelfContained = $true
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$resolvedProjectPath = if ([System.IO.Path]::IsPathRooted($ProjectPath))
{
    $ProjectPath
}
else
{
    Join-Path $repoRoot $ProjectPath
}

$resolvedOutputRoot = if ([System.IO.Path]::IsPathRooted($OutputRoot))
{
    $OutputRoot
}
else
{
    Join-Path $repoRoot $OutputRoot
}

$publishDir = Join-Path $resolvedOutputRoot $Runtime

if (-not (Test-Path -LiteralPath $resolvedProjectPath))
{
    throw "Could not find project file: $resolvedProjectPath"
}

$projectXml = [xml](Get-Content -LiteralPath $resolvedProjectPath -Raw)
$targetFramework = $projectXml.Project.PropertyGroup |
    Where-Object { $_.TargetFramework } |
    Select-Object -First 1 -ExpandProperty TargetFramework
if ([string]::IsNullOrWhiteSpace($targetFramework))
{
    throw "Could not determine TargetFramework from project file: $resolvedProjectPath"
}

$launcherProjectPath = Join-Path $repoRoot "tools/PackageLauncher/PackageLauncher.csproj"
$packageReadmePath = Join-Path $repoRoot "docs/package-readme.md"
if (-not (Test-Path -LiteralPath $launcherProjectPath))
{
    throw "Could not find package launcher project: $launcherProjectPath"
}

if (-not (Test-Path -LiteralPath $packageReadmePath))
{
    throw "Could not find package README: $packageReadmePath"
}

$launcherProjectXml = [xml](Get-Content -LiteralPath $launcherProjectPath -Raw)
$launcherTargetFramework = $launcherProjectXml.Project.PropertyGroup |
    Where-Object { $_.TargetFramework } |
    Select-Object -First 1 -ExpandProperty TargetFramework
if ([string]::IsNullOrWhiteSpace($launcherTargetFramework))
{
    throw "Could not determine TargetFramework from launcher project file: $launcherProjectPath"
}

if (Test-Path -LiteralPath $publishDir)
{
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$buildArgs = @(
    "build"
    $resolvedProjectPath
    "-c"
    $Configuration
    "-r"
    $Runtime
    "--self-contained"
    $SelfContained.ToString().ToLowerInvariant()
)

Write-Host "Building TriloGame.Game for $Runtime..."
& dotnet @buildArgs | Out-Host
if ($LASTEXITCODE -ne 0)
{
    throw "dotnet build failed with exit code $LASTEXITCODE."
}

$projectDir = Split-Path -Parent $resolvedProjectPath
$buildOutputDir = Join-Path $projectDir (Join-Path "bin" (Join-Path $Configuration (Join-Path $targetFramework $Runtime)))
if (-not (Test-Path -LiteralPath $buildOutputDir))
{
    throw "Could not find build output directory: $buildOutputDir"
}

$launcherArgs = @(
    "publish"
    $launcherProjectPath
    "-c"
    $Configuration
    "-r"
    $Runtime
    "--self-contained"
    "true"
    "/p:PublishSingleFile=true"
    "/p:PublishReadyToRun=false"
)

Write-Host "Building package launcher for $Runtime..."
& dotnet @launcherArgs | Out-Host
if ($LASTEXITCODE -ne 0)
{
    throw "dotnet publish for package launcher failed with exit code $LASTEXITCODE."
}

$launcherProjectDir = Split-Path -Parent $launcherProjectPath
$launcherPublishDir = Join-Path $launcherProjectDir (Join-Path "bin" (Join-Path $Configuration (Join-Path $launcherTargetFramework (Join-Path $Runtime "publish"))))
$launcherExePath = Join-Path $launcherPublishDir "The Scuttlers.exe"
if (-not (Test-Path -LiteralPath $launcherExePath))
{
    throw "Could not find package launcher executable: $launcherExePath"
}

$gameFilesDir = Join-Path $publishDir "GameFiles"
New-Item -ItemType Directory -Path $gameFilesDir -Force | Out-Null

Copy-Item -Path (Join-Path $buildOutputDir "*") -Destination $gameFilesDir -Recurse -Force
Copy-Item -LiteralPath $launcherExePath -Destination (Join-Path $publishDir "The Scuttlers.exe") -Force
Copy-Item -LiteralPath $packageReadmePath -Destination (Join-Path $publishDir "README.md") -Force

Write-Host "Package output complete."
Write-Output $publishDir
