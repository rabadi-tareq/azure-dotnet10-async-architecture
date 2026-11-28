# Local Emulator Profile (Item 1, Step 5)

## Concept
The solution normally depends on Azure PaaS services. For $0 local development, each one is replaced by a container that speaks the same protocol:

| Azure service | Local container |
|---|---|
| Service Bus | Service Bus Emulator |
| Blob Storage | Azurite |
| Azure SQL | SQL Server Linux |
| Redis | `redis:7` |
| Application Insights | Aspire Dashboard |

The Service Bus Emulator stores its state in SQL Server, so it depends on the `sqlserver` container being healthy first. Compose handles that ordering with `depends_on` and a healthcheck.

## What was done
1. Created `local/docker-compose.yml` with the five services.
2. Created `local/servicebus-config.json` with the queues `ticket-render-requests` and `ticket-render-completions`.
3. Created `local/.env.example` and a local `local/.env` with the SQL password. `local/.env` is git-ignored.
4. Brought the stack up and verified all five containers.

## Challenges and fixes
| Problem | Cause | Fix |
|---|---|---|
| Containers showed "created" and never started | Port conflicts. | See the two port rows below. |
| Port 1433 already allocated | Another project's `dsg-sqlserver` container uses it. | Host port moved to **1434** (`1434:1433`). |
| Port 5672 already allocated | Another project's `dsg-rabbitmq` container uses it. | Host port moved to **5673** (`5673:5672`). |
| Emulator crashed on start | Queue config used a 14-day message TTL. The emulator allows 1 hour at most. | Changed `DefaultMessageTimeToLive` to `PT1H`. |
| Red `NativeCommandError` text in PowerShell | PowerShell reports Docker's progress output on stderr. | Harmless, ignore it. |

Containers on the same Docker network still talk to each other on the standard ports. Only apps running on the host use 1434 and 5673.

## Local endpoints
- **Service Bus:** `Endpoint=sb://localhost:5673;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;`
- **SQL:** `localhost,1434`
- **Azurite:** `127.0.0.1:10000`
- **Redis:** `localhost:6379`
- **Aspire Dashboard:** http://localhost:18888 (OTLP on `4317`)

## Running the containers
Run from the repo root, one command at a time:

1. Make sure Docker Desktop is running.
2. Start the stack: `docker compose -f local/docker-compose.yml --env-file local/.env up -d`
3. Wait about 30-60 seconds. The Service Bus Emulator starts last.
4. Check it: `docker compose -f local/docker-compose.yml ps`
5. Stop it, keeping the containers: `docker compose -f local/docker-compose.yml stop`
6. Remove it: `docker compose -f local/docker-compose.yml down`

## Things to keep in mind
- `local/.env` is git-ignored. On a fresh clone, copy `local/.env.example` to `local/.env` first.
- The Service Bus Emulator recreates its databases on every start, so queue messages don't survive a restart.
- The queue TTL is 1 hour locally. Azure uses the Bicep defaults.
- `down` removes the containers. Use `stop` to keep them.
