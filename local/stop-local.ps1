#Requires -Version 5.1
<#
.SYNOPSIS
    Stops the three local apps and, optionally, the Docker containers.
    Also deletes the log files the apps wrote to the local folder.
.PARAMETER Containers
    Also stop and remove the Docker containers (data in the containers is lost).
#>
param([switch]$Containers)

$root = Split-Path -Parent $PSScriptRoot

$windows = Get-CimInstance Win32_Process -Filter "Name='powershell.exe'" |
    Where-Object { $_.CommandLine -match 'dotnet run --project src\\AsyncTickets\.' }
foreach ($w in $windows) {
    Stop-Process -Id $w.ProcessId -Force -ErrorAction SilentlyContinue
}

Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" |
    Where-Object { $_.CommandLine -match 'AsyncTickets\.(Web\.CallCenter|TicketRenderer)' } |
    ForEach-Object { Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue }

Write-Host 'Apps stopped.'

Start-Sleep -Seconds 2
Remove-Item (Join-Path $PSScriptRoot '*.log') -Force -ErrorAction SilentlyContinue
Write-Host 'Log files removed.'

if ($Containers) {
    docker compose -f (Join-Path $PSScriptRoot 'docker-compose.yml') --env-file (Join-Path $PSScriptRoot '.env') down
    Write-Host 'Containers stopped.'
}
