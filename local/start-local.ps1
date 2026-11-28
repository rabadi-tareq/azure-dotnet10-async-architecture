#Requires -Version 5.1
<#
.SYNOPSIS
    Starts the local emulator profile: Docker containers plus the three apps.
.PARAMETER SkipDocker
    Do not start/check the Docker containers (assume they are already running).
.PARAMETER NoBrowser
    Do not open the browser when everything is up.
#>
param(
    [switch]$SkipDocker,
    [switch]$NoBrowser
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

function Wait-Until([scriptblock]$Test, [int]$TimeoutSec, [string]$What) {
    $deadline = (Get-Date).AddSeconds($TimeoutSec)
    while ((Get-Date) -lt $deadline) {
        if (& $Test) { return }
        Start-Sleep -Seconds 3
    }
    throw "Timed out waiting for $What."
}

function Test-Url([string]$Url) {
    try { Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 5 | Out-Null; return $true }
    catch { return ($null -ne $_.Exception.Response) }
}

function Start-App([string]$Name, [string]$Project, [string]$LaunchProfile) {
    $log = Join-Path $PSScriptRoot "$Name.log"
    $cmd = "`$Host.UI.RawUI.WindowTitle='$Name'; dotnet run --project $Project --launch-profile $LaunchProfile 2>&1 | Tee-Object -FilePath '$log'"
    Start-Process powershell -ArgumentList '-NoExit', '-Command', $cmd -WorkingDirectory $root | Out-Null
    Write-Host "Started $Name (log: local\$Name.log)"
}

if (-not $SkipDocker) {
    if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
        throw 'Docker is not installed or not on PATH. Install Docker Desktop first.'
    }

    docker info *> $null
    if ($LASTEXITCODE -ne 0) {
        $desktop = Join-Path $env:ProgramFiles 'Docker\Docker\Docker Desktop.exe'
        if (-not (Test-Path $desktop)) { throw 'Docker is not running and Docker Desktop was not found.' }
        Write-Host 'Starting Docker Desktop...'
        Start-Process $desktop
        Wait-Until { docker info *> $null; $LASTEXITCODE -eq 0 } 180 'Docker Desktop'
    }

    $envFile = Join-Path $PSScriptRoot '.env'
    if (-not (Test-Path $envFile)) {
        Copy-Item (Join-Path $PSScriptRoot '.env.example') $envFile
        Write-Host 'Created local\.env from .env.example.'
    }

    Write-Host 'Starting containers...'
    $ErrorActionPreference = 'Continue'
    docker compose -f (Join-Path $PSScriptRoot 'docker-compose.yml') --env-file $envFile up -d 2>&1 | Out-Host
    $composeExit = $LASTEXITCODE
    $ErrorActionPreference = 'Stop'
    if ($composeExit -ne 0) { throw 'docker compose up failed.' }

    Write-Host 'Waiting for SQL Server to be healthy...'
    Wait-Until {
        (docker inspect --format '{{.State.Health.Status}}' asynctickets-local-sqlserver-1 2>$null) -eq 'healthy'
    } 180 'SQL Server'
    Start-Sleep -Seconds 10
}

Start-App 'api' 'src\AsyncTickets.Web.CallCenter.Api' 'AsyncTickets.Web.Api'
Write-Host 'Waiting for the API on http://localhost:5242 ...'
Wait-Until { Test-Url 'http://localhost:5242/swagger/index.html' } 180 'the API'

Start-App 'renderer' 'src\AsyncTickets.TicketRenderer' 'AsyncTickets.TicketRenderer'
Start-App 'web' 'src\AsyncTickets.Web.CallCenter' 'AsyncTickets'
Write-Host 'Waiting for the web app on http://localhost:5227 ...'
Wait-Until { Test-Url 'http://localhost:5227' } 180 'the web app'

Write-Host ''
Write-Host 'Everything is up:'
Write-Host '  App        http://localhost:5227'
Write-Host '  API        http://localhost:5242/swagger'
Write-Host '  Dashboard  http://localhost:18888'

if (-not $NoBrowser) {
    Start-Process 'http://localhost:5227'
    Start-Process 'http://localhost:18888'
}
