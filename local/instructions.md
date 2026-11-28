# Running AsyncTickets Locally ($0 Emulator Profile)

Runs the whole solution on your machine. Docker emulators replace the Azure services, so no Azure account is needed.

## Prerequisites
- Git
- .NET 10 SDK
- Docker Desktop (with WSL2 on Windows)
- PowerShell 5.1 or later

Free ports needed: 1434, 5673, 6379, 10000-10002, 18888, 4317, 5242, 5227, 7242, 7227.

## Start
From the repository root:

```powershell
.\local\start-local.ps1
```

In short, the script:
1. Starts Docker Desktop if needed, then the emulator containers (Service Bus, SQL Server, Azurite, Redis, Aspire Dashboard).
2. Starts the API, the ticket renderer and the web app, each in its own PowerShell window. Logs go to `local\*.log`.
3. Waits until everything responds, then opens the web app and the Dashboard in your browser.

Switches: `-SkipDocker` (containers already running), `-NoBrowser`.

If PowerShell blocks the script, run it once with `powershell -ExecutionPolicy Bypass -File .\local\start-local.ps1`.

## Use it
| What | URL |
|---|---|
| Web app (signed in automatically as "Local Developer") | http://localhost:5227 |
| API (Swagger) | http://localhost:5242/swagger |
| Aspire Dashboard (traces, logs, metrics) | http://localhost:18888 |

Buy a ticket in the web app. The ticket image appears after the renderer finishes. The Dashboard shows the request as one trace across the web app, API and renderer.

## Stop
```powershell
.\local\stop-local.ps1              # stops the three apps and deletes their logs
.\local\stop-local.ps1 -Containers  # also removes the containers (their data is lost)
```

## Troubleshooting
- **Script fails early:** check that Docker Desktop is installed and running.
- **"Port is already allocated":** another program uses one of the ports above. Stop it, or change the mapping in `docker-compose.yml`.
- **An app misbehaves:** read its log in `local\` (`api.log`, `renderer.log`, `web.log`).

## Notes
- Local sign-in and the local password in `.env.example` are for development only. Never use them in Azure.
- `local\.env` is created from `.env.example` on first run and is git-ignored.
- The scripts are the source of truth for how everything starts and stops. Read them for details.
