# Item 1 Step 6: Running the Solution Locally ($0 Emulator Profile)

## Goal
Run `Web.CallCenter`, `Web.CallCenter.Api` and `TicketRenderer` end to end on a developer machine, using Docker emulators instead of Azure, with correlated distributed traces in the Aspire Dashboard.

## What Was Done
- **Local-first configuration:** a connection string, when configured, takes precedence over the Azure keyless (token credential) path for Service Bus and Blob Storage. Redis on localhost skips Azure token auth. Non-local behavior is unchanged.
- **Opt-in local mode:** `App:UseLocalEmulators` in `appsettings.Development.json` enables local mode. Azure App Configuration is only used when its URI is configured.
- **Dev-only authentication:** the API and web app register a `LocalDev` scheme that signs in a fixed "Local Developer" administrator. The web app also gets a stub token provider. Entra ID remains the non-local path.
- **Telemetry:** the three apps export OTLP traces, metrics and logs to the Aspire Dashboard, including Azure SDK and Service Bus spans.
- **Storage:** the renderer creates the `tickets` blob container in Azurite on demand.
- **Packages:** OTLP exporter upgraded from 1.15.0 to 1.21.0 to clear NU1902 vulnerability warnings.

## Challenges and Resolutions
| Challenge | Resolution |
|---|---|
| Options required Azure host/URI values | Made them conditional: either a host/URI or a connection string is valid |
| Startup required Azure App Configuration | Gated it on the URI being present |
| Every endpoint requires authentication | Added the dev-only auth bypass, active only in local mode |
| Making the Service Bus host optional broke an existing test | Replaced the attribute with `IValidatableObject` validation |
| Service Bus emulator port clash with another project | Mapped the emulator to host port 5673 |
| Several automated edits truncated source and project lines | Detected through build errors and repaired |
| `http` launch profile did not exist; apps started in Production on port 5000 | Used the real profile names |
| Trace verification noise from background pollers | Located the `POST api/Ticket/Purchase` trace in the Dashboard |

## Result
- Solution builds; all 53 tests pass.
- A ticket purchase flows Web -> API -> SQL -> Service Bus -> TicketRenderer -> Azurite -> completion queue -> API.
- One Dashboard trace shows both API and renderer spans.

## Running It
1. Start the containers: `docker compose -f local\docker-compose.yml --env-file local\.env up -d`
2. API: `dotnet run --project src\AsyncTickets.Web.CallCenter.Api --launch-profile AsyncTickets.Web.Api`
3. Renderer: `dotnet run --project src\AsyncTickets.TicketRenderer --launch-profile AsyncTickets.TicketRenderer`
4. Web: `dotnet run --project src\AsyncTickets.Web.CallCenter --launch-profile AsyncTickets`
5. App: http://localhost:5227. Traces: http://localhost:18888.

Run each app in its own window.
