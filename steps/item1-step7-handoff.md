# Item 1 Step 7 Hand-off: Final Verification of the Local Profile

Use this file to seed the next chat. Step 6 is complete (see `steps\item1-step6-summary.md`). Step 7 closes the loose ends.

## Big Picture
Master plan Item 1: run `Web.CallCenter`, `Web.CallCenter.Api` and `TicketRenderer` end to end locally at $0, with Docker emulators and correlated traces in the Aspire Dashboard. Branch: `item-1-running-the-solution-locally`. Solution: `AsyncTickets.sln` (.NET 10, Razor Pages front end).

## State at Hand-off
- Five containers run via `local\docker-compose.yml`: Service Bus emulator (host 5673), SQL Server (1434), Azurite (10000-10002), Redis (6379), Aspire Dashboard (UI 18888, OTLP 4317).
- API (http://localhost:5242), renderer (port 5000) and web app (http://localhost:5227) were running in separate PowerShell windows at the time of writing.
- Launch profiles (the profile `http` does not exist): API `AsyncTickets.Web.Api`, renderer `AsyncTickets.TicketRenderer`, web `AsyncTickets`.
- Build is clean, all 53 tests pass.
- Local mode is `App:UseLocalEmulators=true` in each `appsettings.Development.json`. It enables connection-string clients and a dev-only "Local Developer" admin sign-in.
- OTLP exporter was upgraded 1.15.0 to 1.21.0 (NU1902 fix). Tests pass, but traces were not checked afterwards.
- Nothing is committed. The user will commit when they choose; do not commit.

## Step 7 Tasks (the "Still Open" list)
1. **Test the scripts.**
   - `local\stop-local.ps1` (add `-Containers` to also stop containers).
   - `local\start-local.ps1` (switches `-SkipDocker`, `-NoBrowser`). Until now it has only had a syntax check.
   - Fix anything that fails: Docker Desktop launch, `.env` creation, SQL health wait, readiness URLs, window launching, browser opening.
2. **Full browser purchase.** Buy a ticket through the web UI at http://localhost:5227, confirm the 202/redirect, and confirm the ticket image appears after the renderer finishes.
3. **Verify traces on exporter 1.21.0.** In http://localhost:18888, find the `POST api/Ticket/Purchase` trace and confirm it contains both `AsyncTickets.Web.CallCenter.Api` and `AsyncTickets.TicketRenderer` spans.
4. **Review `local\README.md`** against what the script actually does, and correct any mismatch.

Done when tasks 1 to 3 pass. Task 4 is a final check.

## Working Rules
- Give the user ONE terminal command at a time, with `2>&1 | clip` appended so the output is on the clipboard. Wait for the pasted output, verify it, then give the next command.
- `| clip` only captures a command after it exits. For long-running apps, start them with `Start-Process powershell ... | Tee-Object -FilePath local\<name>.log`, then `Start-Sleep` and `Get-Content ... -Tail N 2>&1 | clip`.
- Save markdown files the user asks for in `steps\`.
- Do not change code unless a test fails. Explain before large changes.

## Known Details
- Local passwords: `local\.env` (git-ignored); default `SQL_SA_PASSWORD=Change_Me_Local_Only_123!` must match the API's `appsettings.Development.json`.
- `local\*.log` is git-ignored. `local\` itself is untracked and should be committed (except `.env`).
- Existing build warnings are not from this work: CS8604 in the web `Program.cs`, and CS0618 for `AddDownstreamWebApi`.
- Aspire Dashboard runs unsecured locally. The Dashboard API needs an `x-api-key`, so verify traces in the UI.
- The background pollers create many unrelated traces. Look for the Purchase trace.

## Key Files
- `local\docker-compose.yml`, `local\servicebus-config.json`, `local\.env.example`
- `local\start-local.ps1`, `local\stop-local.ps1`, `local\README.md`
- `steps\item1-step5.md`, `steps\item1-step6-handoff.md`, `steps\item1-step6-summary.md`
- Each app's `appsettings.Development.json` and `Properties\launchSettings.json`
