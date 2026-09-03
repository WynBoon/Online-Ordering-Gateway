# Local development

The hosts start without Azure. You need **Docker Desktop** (for SQL Server). Key Vault and Service Bus are optional: if they are not configured, `local://` secret refs resolve from user secrets, literal Pilot API keys stored on a store are used as-is, and the live ticker / webhook delivery stay idle.

## 1. Start SQL Server

```powershell
docker compose up -d sql
```

Wait until the container is healthy (`docker compose ps`). First pull takes a minute. Connection string is already in each `appsettings.Development.json` (`sa` / `Gateway_LocalDev_1`, database `Gateway` created on first run).

If you already have SQL Server locally, point `ConnectionStrings:Gateway` at that instead.

## 2. Build and test

From the repo root:

```powershell
dotnet test Gateway.slnx
```

## 3. Run the API

```powershell
dotnet run --project src/Gateway.Api --launch-profile http
```

On first start it applies the EF migration and seeds two stores. Swagger: http://localhost:5175/swagger

| Store | State | Bearer location key |
|---|---|---|
| Local Dev Kitchen | Active | `dev-local-location-key` |
| Local Dev Cafe (Paused) | Paused | `dev-paused-location-key` |

`src/Gateway.Api/Gateway.Api.http` has sample `GET /health`, `GET /menu`, and `POST /orders` calls.

`GET /health` authenticates and hits the database. It returns **503** until the store's Pilot key works (paste it on the store page in the portal, or set user secrets for the seeded `local://pilot-api-key` ref):

```powershell
dotnet user-secrets set "LocalSecrets:pilot-api-key" "<pilot-global-api-key>" --project src/Gateway.Api
```

Repeat for `src/Gateway.Portal` and `src/Gateway.Worker` if you run those.

## 4. Run the portal (optional)

```powershell
dotnet run --project src/Gateway.Portal --launch-profile http
```

http://localhost:5083 — command centre and store list, no Entra sign-in until `AzureAd:ClientId` is set. The live ticker stays empty without Service Bus.

Open a store (or **Onboard store**) and paste the Pilot API key on the POS card. **Test connection** calls QA `POST /Authorization/Token` (`Pilot:BaseUrl` in `appsettings.Development.json`), fills vendor/store from the response, and shows permissions. Save writes the API key onto `PosConnection` — not a Key Vault URI. Seeded stores still use `local://pilot-api-key` until you replace it there.

## 5. Run the worker (optional)

Needs [Azure Functions Core Tools](https://learn.microsoft.com/azure/azure-functions/functions-run-local) and [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) (`npx azurite` is enough).

```powershell
copy src\Gateway.Worker\local.settings.json.example src\Gateway.Worker\local.settings.json
cd src/Gateway.Worker
func start
```

The Service Bus-triggered webhook function is **disabled** locally (`AzureWebJobs.OutboundWebhookDeliveryFunction.Disabled=true`). Timer functions (outbox, GAAP poll, menu re-pull) still run; outbox dispatch no-ops until `ConnectionStrings:ServiceBus` is set.

To enable webhook delivery locally: stand up a Service Bus topic `order-events` with subscriptions `webhook-delivery` (sessions on) and `portal-live-feed`, set the connection string, and set `AzureWebJobs.OutboundWebhookDeliveryFunction.Disabled` to `false`.

## What still needs sandboxes

POS credentials (unless you paste a Pilot key on the store page), Order Harmony webhook URLs, and the 14 certification tests. Seeding and Docker SQL only get the processes running.
