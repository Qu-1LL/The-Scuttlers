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

if (Test-Path -LiteralPath $publishDir)
{
    Remove-Item -LiteralPath $publishDir -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

$publishArgs = @(
    "publish"
    $resolvedProjectPath
    "-c"
    $Configuration
    "-r"
    $Runtime
    "--self-contained"
    $SelfContained.ToString().ToLowerInvariant()
    "-o"
    $publishDir
)

Write-Host "Publishing TriloGame.Game to $publishDir..."
& dotnet @publishArgs | Out-Host
if ($LASTEXITCODE -ne 0)
{
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

Write-Host "Publish complete."
Write-Output $publishDir
