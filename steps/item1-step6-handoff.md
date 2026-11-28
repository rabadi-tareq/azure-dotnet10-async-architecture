# Item 1, Step 6 - Handoff Context

## Working rules (from the user)
- Follow the `step-by-step-workflow` skill (`C:\Users\Tareq\.copilot\skills\step-by-step-workflow\SKILL.md`): one step at a time, minimal responses, wait for explicit prompts.
- For Item 1 only, the user has delegated technical fixes: no approval needed for fixes needed to get the local setup working.
- When asking the user to run terminal commands: one command at a time, append `2>&1 | clip` (user can't copy terminal output), run in Developer PowerShell at the repo root.
- PowerShell shows Docker progress as `NativeCommandError`. It is harmless.

## Objective
Item 1 of `repository_development_hardening_plan.md` (in Downloads): run the full flow locally at $0 using emulators. `Web.CallCenter`, `Web.CallCenter.Api` and `TicketRenderer` must run end to end, with correlated traces in the Aspire Dashboard.

## Repo state
- Repo: `C:\Users\Tareq\Documents\dev\azure-dotnet10-async-architecture`, branch `item-1-running-the-solution-locally` (the .NET 10 work is already merged to `main`).
- Projects are under `src\`: `AsyncTickets.Web.CallCenter`, `AsyncTickets.Web.CallCenter.Api`, `AsyncTickets.TicketRenderer`, `AsyncTickets.Messaging`.
- Done so far (Steps 1-5), uncommitted:
  - `local/docker-compose.yml`
  - `local/servicebus-config.json`
  - `local/.env.example`
  - `local/.env` (git-ignored)
  - `local/item1-step5.md`
  - `.gitignore` edit

## Step 5 result: emulator stack is running
Start it with: `docker compose -f local/docker-compose.yml --env-file local/.env up -d`

| Service | Host endpoint |
|---|---|
| Service Bus Emulator | `Endpoint=sb://localhost:5673;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;` |
| SQL Server | `localhost,1434` (user `sa`, password in `local/.env`) |
| Azurite | `127.0.0.1:10000` (default dev account `devstoreaccount1`) |
| Redis | `localhost:6379` |
| Aspire Dashboard | UI http://localhost:18888, OTLP `http://localhost:4317` |

- Ports 1434 and 5673 are non-default because another project's `dsg-sqlserver` and `dsg-rabbitmq` containers hold 1433 and 5672.
- Queues: `ticket-render-requests`, `ticket-render-completions`. TTL is 1h (emulator maximum).

## Step 2 inventory: where code assumes Azure
- **API `Program.cs`:** registers services only if `App:AppConfig:Uri` is set (`hasRequiredConfigSettings`). Otherwise it serves a "missing settings" page. `AddAzureAppConfiguration` and `UseAzureAppConfiguration` need the URI.
- **API `Extensions/ServiceCollectionExtensions.cs`:**
  - `AddCallCenterApiServices` takes the credential from `GetAzureCredential(configuration)`.
  - Service Bus: `AddAzureServiceBusMessageBus("App:ServiceBus", credential)`.
  - Blob: `AddTicketImageService` builds `BlobServiceClient(new Uri(App:StorageAccount:Uri), token)` and throws if the URI is missing.
  - Redis: `AddDistributedSession` uses `ConfigureForAzureWithTokenCredentialAsync` when `App:RedisCache:ConnectionString` is set, otherwise in-memory cache.
  - SQL: `App:SqlDatabase:ConnectionString`, plain connection string with a mock fallback.
  - Telemetry: App Insights OpenTelemetry only when `App:ApplicationInsights:ConnectionString` is set.
  - Auth: `AddMicrosoftIdentityWebApiAuthentication(configuration, "Api:MicrosoftEntraId")`.
- **`AsyncTickets.Messaging/ServiceBus/AzureServiceBusMessageBusExtensions.cs`:** `new ServiceBusClient(options.Host, azureCredential ?? new DefaultAzureCredential(), clientOptions)`. `MessageBusOptions` (`MessageBusOptions.cs`) has `Host`, `RenderRequestQueueName`, `RenderCompleteQueueName`, validated at startup.
- **`TicketRenderer/Extensions.cs`:**
  - `AddAzureServices` uses `AddAzureServiceBusMessageBus` plus `AddBlobServiceClient(new Uri(storageOptions.Uri))` with the shared credential. `AzureStorageOptions.Uri` is required.
  - `AddAzureAppConfiguration` runs only if `App:AppConfig:Uri` is set.
  - `AddTelemetry` uses `UseAzureMonitor` only (no OTLP).
- **Web.CallCenter (front end):** uses Entra sign-in. Not yet inspected in detail (`Program.cs`, `Extensions/ServiceCollectionExtensions.cs`).

## Step 3 design (approved)
1. **Switch** is by which settings are present, with no global flag. If a connection string is set, use it. Otherwise the current keyless `TokenCredential` path is unchanged.
2. **Service Bus:** `new ServiceBusClient(connectionString, clientOptions)` when `App:ServiceBus:ConnectionString` is set. `MessageBusOptions.Host` becomes optional in that case.
3. **Blob:** `new BlobServiceClient(connectionString)` when `App:StorageAccount:ConnectionString` is set, in both the API and the renderer. Relax `AzureStorageOptions.Uri`.
4. **Redis:** skip `ConfigureForAzureWithTokenCredentialAsync` for a plain `localhost` connection string.
5. **API startup gate:** replace `hasRequiredConfigSettings` with "App Config URI set, or a local settings marker present". Only call `AddAzureAppConfiguration` and `UseAzureAppConfiguration` when the URI exists. The feature flag `DistributedTicketRendering` comes from `FeatureManagement` in `appsettings.Development.json`.
6. **Telemetry:** add the OTLP exporter package to the API and the renderer. Register it when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, with the Service Bus and `Azure.*` sources for correlated traces. Keep Azure Monitor when an App Insights connection string is set.
7. **Config:** add local values to each project's `appsettings.Development.json`: SQL, Redis, Service Bus, Storage, the feature flag and queue names.

## Open risk to decide in its own step
The API uses Microsoft Entra authentication (`AddMicrosoftIdentityWebApiAuthentication`) and the front end uses Entra sign-in. Local mode needs either a dev-only auth bypass or the user's own app registration.

## Next
Step 6: implement the C# changes from the Step 3 design. Build and tests must stay green: 53 tests passed at the end of Phase 1. Then continue with the remaining Item 1 steps:
- auth decision
- run the three apps locally
- verify the end-to-end flow and the traces in the Aspire Dashboard
- commit
