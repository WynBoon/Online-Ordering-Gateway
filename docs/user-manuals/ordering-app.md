# Ordering app (Order Harmony stand-in)

Who it's for: anyone exercising the **channel** without Dine Direct — pull the till menu, place a prepaid test order, watch the status ladder. Kitchen staff still use the [in-store device](in-store-device.md).

This MAUI app **pretends to be Order Harmony**. It talks only to `Gateway.Api` with a Bearer **location key**. It does not pair as a store tablet and does not call the portal.

## Connect

1. Open **Ordering.App** (Windows or Android). Gateway URL defaults to UAT: `https://moog-api-cehvddbad6c0f8gd.southafricanorth-01.azurewebsites.net`.
2. Paste the **UAT store location key** from the portal (store → channel card). `dev-local-location-key` is local Docker only and will 401 against UAT.
3. `GET /health` **200** means the till path is up. **503** still means the location key was accepted — the POS ping failed. **401** is a wrong key.

For a local API instead, type `http://localhost:5175` (Android emulator: `http://10.0.2.2:5175`) and use `dev-local-location-key`.

## Menu, options, checkout

- **Menu** is `GET /menu`. Product and modifier `external_id`s are till PLUs and must go back on the order. Against Pilot, top-level `MODIFY` catalogue rows are already filtered out — options appear under each product.
- Opening an item shows modifier groups with min/max. The app enforces those rules before add-to-cart (the gateway does not yet return `422`).
- Checkout is always **prepaid** (Channel contract). Pick pickup / delivery / dine-in. Delivery requires a street address.
- Place order sends `POST /orders` with a new `Idempotency-Key`. A retryable `503` is retried with the **same** key. Failed injects are not sticky — a corrected retry may reuse the key.

## Status

After a `201`, **Order status** polls `GET /orders/{order_ref}` about every two seconds until `completed` or `cancelled`.

```
accepted → preparing → ready → completed
                ↘         ↘        ↘
                 cancelled
```

The first status is **accepted** on inject. Preparing / ready / completed come from **Store Device** (or a Pilot callback when the API has `Pilot:CallbackBaseUrl` set). This app does not call `/device/*`.

`GET /orders` lists recent tickets for this location key (**My orders**). These GETs are a stand-in convenience; production Order Harmony still receives signed webhooks.

## Lab

The Lab flyout is for Channel certification helpers: replay the last `Idempotency-Key`, submit an unknown PLU, and hit the seeded paused store (`dev-paused-location-key`). Hide it before any diner-facing build.

## Build

Same dual-target as Store Device so `dotnet test Gateway.slnx` stays green without the MAUI workload.

```powershell
dotnet build src/Ordering.App/Ordering.App.csproj -f net10.0-windows10.0.19041.0 -p:BuildOrderingMaui=true
dotnet build src/Ordering.App/Ordering.App.csproj -f net10.0-android -p:BuildOrderingMaui=true
```
