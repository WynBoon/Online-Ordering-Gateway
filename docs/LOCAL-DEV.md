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

**Pilot menu:** `GET /menu` reshapes Pilot's `{ PluItems: [...] }` into OH categories. Prices are major units on Pilot → **cents** on the channel. Top-level rows with `Dtab = MODIFY` are the till's modifier catalogue and are **excluded** — sellable modifiers come from each product's `Options`.

**Pilot callbacks:** inject sends `callbackUrl` only when `Pilot:CallbackBaseUrl` is set (env `Pilot__CallbackBaseUrl`). Value should be a public base the till can reach (ngrok / App Service), e.g. `https://your-host` → Pilot posts to `{base}/pilot/callback/{orderRef}`. Empty (the local default) means **no callback URL** — after **accepted**, walk status with the [in-store device](user-manuals/in-store-device.md) instead.

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

## 6. In-store device (MAUI)

The tablet is the status of record for preparing / ready / completed. The till still gets the kitchen ticket. Pairing codes are issued by the portal against the database; the tablet talks only to the API (port 5175), never to the portal (port 5083).

1. Run the API (`http://localhost:5175`) and portal (`http://localhost:5083`).
2. Open a store → **In-store devices** → tick functions → **Issue pairing code**. The six-digit code is shown once and expires in 15 minutes.
3. On a machine with the MAUI workload:

In **Visual Studio**, close and reopen the solution, set **StoreDevice.App** as the startup project, then pick an **Android emulator** or **Windows Machine** from the debug dropdown. The app targets `net10.0-android` / `net10.0-windows` to match the installed MAUI workloads (a `net8.0-android` target will not appear).

From the CLI:

```powershell
dotnet build src/StoreDevice.App/StoreDevice.App.csproj -f net10.0-windows10.0.19041.0 -p:BuildStoreDeviceMaui=true
dotnet build src/StoreDevice.App/StoreDevice.App.csproj -f net10.0-android -p:BuildStoreDeviceMaui=true
```

Without the MAUI workload, CLI `dotnet test Gateway.slnx` still compiles StoreDevice.App as an empty net8.0 library so gateway tests stay green.

The onboarding **Gateway URL** defaults to UAT (`https://moog-api-cehvddbad6c0f8gd.southafricanorth-01.azurewebsites.net`). For a local API on the emulator use `http://10.0.2.2:5175`. Pairing codes must come from the same environment's portal.

4. The device calls `POST /device/enroll`, then heartbeat / orders / actions. Status walks the legal ladder through `StatusSyncUseCase` and Order Harmony webhooks.

If the Android emulator times out pairing to UAT: Chrome in the emulator and open `https://moog-api-cehvddbad6c0f8gd.southafricanorth-01.azurewebsites.net/device/enroll` — if that also hangs, cold-boot the AVD (or debug **Windows Machine** instead). The emulator cannot use a Windows loopback proxy (Fiddler / corporate proxy on 127.0.0.1).

## 7. Ordering app (Harmony stand-in, MAUI)

This is **not** the kitchen tablet. `Ordering.App` pretends to be Order Harmony: `GET /menu` (items + modifier options), `POST /orders`, then polls `GET /orders/{order_ref}` for `accepted → preparing → ready → completed`. Kitchen taps still happen on Store Device.

The Connect screen defaults to UAT (`https://moog-api-cehvddbad6c0f8gd.southafricanorth-01.azurewebsites.net`). A leftover localhost session is rewritten to UAT on load. Paste the **UAT store location key** from the portal (not `dev-local-location-key`).

1. In **Visual Studio**, set **Ordering.App** as the startup project, then pick **Windows Machine** or an **Android emulator**. Same `net10.0-*` / `BuildOrderingMaui` trick as Store Device.

```powershell
dotnet build src/Ordering.App/Ordering.App.csproj -f net10.0-windows10.0.19041.0 -p:BuildOrderingMaui=true
dotnet build src/Ordering.App/Ordering.App.csproj -f net10.0-android -p:BuildOrderingMaui=true
```

Connect → paste the UAT location key → menu from the till → checkout. Walk status on Store Device against the same UAT store. For a local API instead, type `http://localhost:5175` (emulator: `http://10.0.2.2:5175`) and use `dev-local-location-key`.

See `docs/user-manuals/ordering-app.md`.
