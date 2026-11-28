# Master Architecture & Senior Hardening Plan: `azure-dotnet10-async-architecture`

* **Upstream Baseline:** `Azure/modern-web-app-pattern-dotnet` (Relecloud concert ticketing app)
* **Baseline Tag:** `v0-baseline`

---

## 1. Core Objective & Working Model

* **Primary Objective:** Build hands-on understanding of Azure service-to-service interactions, IAM/security boundaries, runtime failure modes, and Bicep IaC. Updating the repository is the tangible output of that learning process.
* **Portfolio Artifact:** Elevate the happy-path Microsoft sample via a bounded **5-item Senior Hardening Spec**, documenting major architectural choices in concise **Architecture Decision Records (ADRs)** under `/docs/adr/` alongside a trade-offs README.

### Division of Labor & 4-Step Execution Loop

* **Architect & Scope Controller (AI):** Enforces scope, breaks down Azure service interactions and failure modes, and audits Bicep/C# diffs for Azure-specific gotchas.
* **In-IDE Implementer (GitHub Copilot):** Executes discrete, tightly scoped code and Bicep tasks, with manual review of every diff.

For every hardening item, follow the **"Understanding First, Code Second"** loop:

1. **Service & Interaction Breakdown:** Analyze control vs. data plane, sync vs. async flows, auth boundaries, and production failure modes before editing code.
2. **Replacement & Hardening Decision:** Evaluate modern Azure replacements for deprecated/weak components and lock in the target configuration.
3. **Scoped Copilot Prompt:** Translate the decision into a concrete, bounded task for GitHub Copilot.
4. **Audit:** Inspect the Bicep and C# diff for Azure-specific gotchas and security gaps.

---

## 2. Execution Profiles & Cost-Control Guardrails

The repository uses **two runtime profiles** so day-to-day development stays at **\$0** while cloud verification stays **under \$1.00 per short session** (Azure PaaS resources bill hourly, so live Azure sessions are never strictly \$0.00):

1. **Local-First Emulator Profile (\$0, 100% Offline):** Runs the full distributed flow locally via Docker Compose (Azurite, Azure Service Bus Emulator, SQL Server Linux, Redis, and the local .NET Aspire Dashboard), replacing cloud-only config/secret services with `appsettings.Development.json` and `dotnet user-secrets`.
2. **Azure Dev Profile (Short-Lived, <\$1.00 per 4-Hour Session):** Used *only* to verify capabilities emulators cannot simulate—real Entra ID Managed Identities, RBAC role assignments, `disableLocalAuth: true`, Bicep deployment, and Container Apps KEDA autoscaling.

### Mandatory Cost Safeguards

* **Enterprise Networking Bypassed in `dev`:** Verified that `environmentType = 'dev'` (`deployHubNetwork = 'auto'`, `networkIsolation = 'auto'`) evaluates `isNetworkIsolated` and `willDeployHubNetwork` to `false`, skipping DDoS Protection (\~\$2,944/mo), Azure Firewall (\~\$912/mo), and Azure Bastion (\~\$140+/mo).
* **Minimal Dev SKUs (\~4-hour East US estimates):**
  * **Azure App Service (Linux Basic `B1`):** \~\$13.14/mo → **\~\$0.07** for 4 hours *(Windows `B1` is \~\$54.75/mo → \~\$0.30 for 4 hours)*.
  * **Azure Managed Redis (Balanced `B0`, 1-Node Non-HA):** \~\$13.14/mo → **\~\$0.07** for 4 hours.
  * **Azure Service Bus (`Standard`):** \$0.0135/hr base → **\~\$0.05** for 4 hours *(Basic tier is \$0.05/million ops → \~\$0.00 if only queues are used)*.
  * **Azure SQL Database (`Basic`, 5 DTU / `Standard`):** \~\$4.90/mo → **\~\$0.03** for 4 hours.
  * **Azure Front Door (`Standard` in `dev`):** \~\$0.05/hr.
  * **Container Apps (Consumption), Storage, Key Vault, App Config (Free), App Insights (first 5 GB/mo free):** **\$0.00 – \$0.05** for low dev traffic.
* **Strict Teardown:** Always run `azd down` immediately after cloud verification sessions, backed by nightly resource-group deletion as a safety net.

---

## 3. Architecture & Local Emulator Reference

### Service-to-Emulator Mapping

| Azure Service | Role in Relecloud | Local Emulator / Alternative (\$0) | Cloud-Only Capability (Needs Live Azure Test) |
| :--- | :--- | :--- | :--- |
| **Azure App Service** (Web + API) | Hosts `Relecloud.Web.CallCenter` (UI) and `Relecloud.Web.CallCenter.Api` (backend API). | Run natively via Visual Studio / `dotnet run` or Docker. | Managed Identity token endpoint (IMDS), runtime stack config. |
| **Azure Container Apps** | Hosts the extracted `Relecloud.TicketRenderer` Linux worker and scales replicas via KEDA based on Service Bus queue depth. | Run locally as a console worker or Docker container. | KEDA queue-depth autoscaling & scale-to-zero behavior. |
| **Azure Container Registry (ACR)** | Stores the `Relecloud.TicketRenderer` Docker image pulled by Container Apps. | Local Docker daemon image cache. | `AcrPull` RBAC role grant for Container Apps UAMI. |
| **Azure Service Bus** | Decouples the API from `TicketRenderer` using a request queue and a response queue. | **Official Azure Service Bus Emulator** (Docker + SQL Server Linux backend, port `5672`, declarative `config.json`). | Entra ID RBAC auth (`disableLocalAuth: true`), KEDA trigger integration. |
| **Azure Blob Storage** | Stores generated concert ticket images written by `TicketRenderer` and read by the API. | **Azurite** (local). | `Storage Blob Data Contributor` RBAC, `allowSharedKeyAccess: false`. |
| **Azure SQL Database** | Primary relational store for concerts, orders, and ticket metadata. | **SQL Server Linux Docker container** (shared with Service Bus Emulator) or LocalDB. | Passwordless Entra ID authentication via Managed Identity. |
| **Cache for Redis → Managed Redis** | Read-through distributed cache for concert listings to reduce SQL load. | **Standard Redis Docker container** (`redis:7` / `redis:8`). | Entra ID Access Policies & token refresh via `Microsoft.Azure.StackExchangeRedis`. |
| **Azure App Configuration** | Centralizes non-secret settings and **Feature Flags** (`Microsoft.FeatureManagement`). | `appsettings.Development.json` (same `IConfiguration` + `IFeatureManager` API). | Dynamic runtime feature-flag refresh without app restart. |
| **Azure Key Vault** | Stores residual application secrets and certificates. | `dotnet user-secrets`. | Key Vault RBAC (`Key Vault Secrets User`) & secret reference resolution. |
| **Application Insights** | Collects logs, metrics, and end-to-end distributed traces across HTTP + Service Bus. | **Local .NET Aspire Dashboard** container (OpenTelemetry OTLP receiver). | Azure Monitor / Application Map & Log Analytics queries. |
| **Azure Front Door** | Global reverse proxy, TLS termination, and WAF. | Omitted locally (direct `localhost` HTTP). | Kept as `Standard` in `dev` Bicep; torn down with `azd down`. |

### End-to-End Business Workflow (Ticket Image Rendering)

**Use Case:** *A customer or agent requests a concert ticket image for an upcoming Relecloud concert.*

1. **Synchronous Ingress (`App Service Web` → `App Service API`):** The web frontend sends an HTTP request to `Relecloud.Web.CallCenter.Api` for concert/ticket data.
2. **Read-Through Caching (`API` → `Redis` → `Azure SQL`):** For concert metadata, the API checks **Redis** first. On a cache miss, it queries **Azure SQL Database**, populates Redis, and returns the data.
3. **Runtime Strangler-Fig Toggle (`API` → `App Configuration`):** When a ticket image is requested, the API checks a feature flag (`Microsoft.FeatureManagement` / `DistributedTicketRendering`) loaded from **Azure App Configuration**:
   * **Flag Off (legacy path):** Renders the ticket image synchronously in-process inside the API.
   * **Flag On (modernized path):** Returns placeholder data immediately to the UI and offloads rendering asynchronously.
4. **Async Decoupling (`API` → `Service Bus Request Queue`):** The API publishes a render-request message (carrying the `traceparent` header for distributed tracing) to the **Azure Service Bus** request queue.
5. **Event-Driven Scaling & Execution (`Service Bus` → `Container Apps` → `Blob Storage`):** **Azure Container Apps** monitors the request queue depth via KEDA (not 1-to-1 per message; a single replica pulls continuously and KEDA scales out when queue depth exceeds the configured `messageCount` threshold up to `maxReplicas`), pulls the image from **ACR**, renders the ticket image with `SkiaSharp`, and uploads the binary to **Azure Blob Storage**.
6. **Async Completion (`Container Apps` → `Service Bus Response Queue` → `API`):** The worker sends a completion message to the **Service Bus** response queue so the API/database reflects that the rendered ticket image is ready in Blob Storage.
7. **Observability & Secrets (`Application Insights` + `Key Vault`):** Every hop emits correlated telemetry to **Application Insights** (or the local Aspire Dashboard) so HTTP request, queue wait time, and Container App render appear as a single distributed trace. Residual secrets are pulled from **Key Vault** via Managed Identity.

---

## 4. Baseline Infrastructure Audit Findings (`v0-baseline`)

| Bicep File(s) Audited | Current State / Findings | Action Required |
| :--- | :--- | :--- |
| `infra/main.parameters.json`, `infra/main.bicep` | `environmentType = 'dev'` bypasses DDoS, Firewall, and Bastion modules. | None (verified safe). |
| `infra/modules/shared-frontdoor.bicep`, `infra/core/security/front-door-with-waf.bicep` | Deploys unconditionally, tightly wired into app routes/scripts, but drops to `Standard_AzureFrontDoor` in `dev`. | Keep intact; tear down with `azd down`. |
| `infra/core/database/azure-cache-for-redis.bicep` | Provisions deprecated `Microsoft.Cache/redis@2023-08-01` (`Basic` in `dev`). Attaches Entra ID `accessPolicyAssignments`, but does **not** set `disableAccessKeyAuthentication`. | **Migrate** to Azure Managed Redis & disable access keys (Hardening Item 2). |
| `infra/modules/application-resources.bicep` | Dev SKUs are cost-appropriate (App Service `B1`, SQL `Standard`, ACR `Basic`). Service Bus uses AVM `avm/res/service-bus/namespace:0.2.3` with `disableLocalAuth: true`. | Audit remaining resources for `disableLocalAuth` / keyless posture (Hardening Item 3). |

---

## 5. Senior Hardening Roadmap

### Item 1: 100% Local Docker Compose + Emulator Profile

**Status: COMPLETE** (see `steps/item1-step7-summary.md`)

* Build a Docker Compose profile wiring Azurite, Azure Service Bus Emulator, SQL Server Linux, Redis, and the local .NET Aspire Dashboard.
* Verify `Web.CallCenter`, `Web.CallCenter.Api`, and `Relecloud.TicketRenderer` run end-to-end locally with correlated distributed traces at \$0 cost.

### Item 2: Migrate Azure Cache for Redis → Azure Managed Redis (Keyless)

* Replace `infra/core/database/azure-cache-for-redis.bicep` (`Microsoft.Cache/redis`) with **Azure Managed Redis** (`Microsoft.Cache/redisEnterprise`, Balanced `B0` 1-node non-HA for `dev`).
* Disable access keys and bind Redis Access Policies to the API's User-Assigned Managed Identity.
* Update `Relecloud.Web.CallCenter.Api` to use `Microsoft.Azure.StackExchangeRedis` for Entra ID token acquisition/refresh.
* Write **`/docs/adr/ADR-001.md`** documenting the migration decision, alternatives rejected, and cost/SDK trade-offs (see template in Section 7).

### Item 3: Security & IAM Boundary Hardening

* Audit all remaining resources in `infra/` to enforce a zero-key posture (`disableLocalAuth: true`, Blob Storage `allowSharedKeyAccess: false`, Key Vault RBAC, SQL passwordless Entra ID, ACR `AcrPull`) and verify least-privilege data-plane RBAC.

### Item 4: Service Bus & Runtime Reliability Hardening

* Harden the async ticket-rendering pipeline for production failure modes: poison message handling, Dead-Letter Queues (DLQ), message lock durations, and consumer idempotency.

### Item 5: Operational Readiness & Async Scale-Out Verification

* **CI/CD IaC Validation:** Add automated Bicep `what-if` validation in CI while preserving low-cost `dev` parameters.
* **Load Testing (\~400–500 Concurrent Requests):** Test the built-in bulk ticket purchase scenario with `DistributedTicketRendering` off (synchronous bottleneck) vs. on (async Service Bus + KEDA scale-out) using a lightweight CLI load runner (k6, PowerShell, or a small .NET console script).
* **Observability:** Verify queue depth, replica scale-out, and end-to-end traces in Application Insights / Azure Monitor (cloud) and the .NET Aspire Dashboard (local).

---

## 6. Explicit Out of Scope & Guardrails

* **No Custom Load/Monitoring UI:** Do not add load-test triggers or progress dashboards into the production web app UI; keep load generation in CLI scripts and monitoring in Azure Monitor / Aspire Dashboard.
* **No Premature Front Door Refactoring:** Keep `Azure Front Door` as-is in `dev` Bicep (`Standard` tier) and rely on `azd down` teardown.
* **No Production Hub Networking in Dev:** Keep DDoS Protection, Azure Firewall, and Bastion disabled in `dev`.
* **Strict Scope Cap:** Keep work bounded to the 5 hardening items above to avoid scope creep.

---

## 7. Reference ADR Template (`/docs/adr/ADR-001.md`)

```markdown
# ADR-001: Migrate from Azure Cache for Redis to Azure Managed Redis

## Status
Accepted

## Context
The upstream `Azure/modern-web-app-pattern-dotnet` baseline provisions legacy **Azure Cache for Redis** (`Microsoft.Cache/Redis`) for distributed caching in `Relecloud.Web.CallCenter.Api`. Microsoft has deprecated Azure Cache for Redis in favor of **Azure Managed Redis** (`Microsoft.Cache/redisEnterprise`), which runs on the Redis Enterprise stack and supports Microsoft Entra ID authentication natively.

## Decision
Replace the `Microsoft.Cache/Redis` Bicep module with **Azure Managed Redis** (`Microsoft.Cache/redisEnterprise`, Balanced B0 SKU for dev) and configure the API to authenticate via its User-Assigned Managed Identity (Entra ID) rather than Redis access keys.

## Alternatives Considered
1. **Keep legacy Azure Cache for Redis:** Rejected. Deploying a deprecated resource type in a 2026 portfolio signals outdated Azure knowledge and relies on legacy access-key patterns.
2. **Remove distributed caching entirely:** Rejected. Retaining the cache layer preserves the multi-service read-through caching pattern and demonstrates keyless Entra ID authentication to Redis.

## Consequences & Trade-Offs
* **Security:** Eliminates Redis access keys from Key Vault/App Service configuration; access is governed by Redis Access Policies bound to the App Service Managed Identity.
* **Code/SDK Impact:** Requires updating the .NET client configuration to use `Microsoft.Azure.StackExchangeRedis` for Entra ID token acquisition and refresh.
* **Cost:** Azure Managed Redis has a higher hourly floor than the legacy Basic C0 tier, requiring strict `azd down` teardown after testing sessions.
```
