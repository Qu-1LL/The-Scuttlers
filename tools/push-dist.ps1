[CmdletBinding()]
param(
    [string]$Branch = "dist",
    [string]$Remote = "origin",
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-Git
{
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [Parameter(Mandatory = $true)]
        [string]$RepositoryRoot
    )

    & git -C $RepositoryRoot @Arguments
    if ($LASTEXITCODE -ne 0)
    {
        throw "git $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
$publishScript = Join-Path $PSScriptRoot "publish-game.ps1"
$publishDir = (& $publishScript -Runtime $Runtime -Configuration $Configuration).Trim()
$publishDir = (Resolve-Path $publishDir).Path

$sourceCommit = (& git -C $repoRoot rev-parse --short HEAD).Trim()
if ($LASTEXITCODE -ne 0)
{
    throw "Could not resolve the current git commit."
}

$branchExists = $false
& git -C $repoRoot ls-remote --exit-code --heads $Remote $Branch *> $null
if ($LASTEXITCODE -eq 0)
{
    $branchExists = $true
}
elseif ($LASTEXITCODE -ne 2)
{
    throw "Could not query $Remote/$Branch."
}

$worktreePath = Join-Path ([System.IO.Path]::GetTempPath()) ("the-scuttlers-dist-" + [Guid]::NewGuid().ToString("N"))
$originalLocation = Get-Location

try
{
    if ($branchExists)
    {
        Write-Host "Fetching $Remote/$Branch..."
        Invoke-Git -RepositoryRoot $repoRoot -Arguments @("fetch", $Remote, $Branch, "--depth", "1")
        Invoke-Git -RepositoryRoot $repoRoot -Arguments @("worktree", "add", "--force", "--detach", $worktreePath, "FETCH_HEAD")
    }
    else
    {
        Write-Host "Creating a new $Branch branch from an orphan commit..."
        Invoke-Git -RepositoryRoot $repoRoot -Arguments @("worktree", "add", "--force", "--detach", $worktreePath, "HEAD")
    }

    Set-Location $worktreePath

    if (-not $branchExists)
    {
        $temporaryBranch = "__dist_publish_" + [Guid]::NewGuid().ToString("N")
        & git switch --orphan $temporaryBranch
        if ($LASTEXITCODE -ne 0)
        {
            throw "Could not create a temporary orphan branch for $Branch."
        }
    }

    Get-ChildItem -LiteralPath $worktreePath -Force |
        Where-Object { $_.Name -ne ".git" } |
        Remove-Item -Recurse -Force

    Get-ChildItem -LiteralPath $publishDir -Force |
        ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $worktreePath -Recurse -Force }

    & git add --all
    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not stage the dist files."
    }

    $status = & git status --short
    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not inspect the dist worktree status."
    }

    if (-not [string]::IsNullOrWhiteSpace(($status -join [Environment]::NewLine)))
    {
        & git commit -m "Publish dist from $sourceCommit"
        if ($LASTEXITCODE -ne 0)
        {
            throw "Could not commit the dist branch contents."
        }
    }
    else
    {
        Write-Host "$Branch is already up to date."
    }

    Write-Host "Pushing published build to $Remote/$Branch..."
    & git push $Remote "HEAD:refs/heads/$Branch"
    if ($LASTEXITCODE -ne 0)
    {
        throw "Could not push to $Remote/$Branch."
    }

    Write-Host "dist push complete."
}
finally
{
    Set-Location $originalLocation

    if (Test-Path -LiteralPath $worktreePath)
    {
        & git -C $repoRoot worktree remove --force $worktreePath *> $null

        if (Test-Path -LiteralPath $worktreePath)
        {
            Remove-Item -LiteralPath $worktreePath -Recurse -Force
        }
    }
}
