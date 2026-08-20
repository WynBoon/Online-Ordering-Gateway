# API Gateway — Architecture

## 1. What this is

A gateway that sits between **Dine Direct / Order Harmony** (the online-ordering
hub, owned by a partner company) and multiple **point-of-sale platforms**
(owned/operated by our merchants, credentials held by us). Order Harmony
already aggregates channels (Uber Eats, Direct Dine, etc.) upstream of us — from
its point of view, we are one "POS partner" implementing its integration spec.
Our job is to be that partner for every restaurant we onboard, then translate
its canonical order into whatever the merchant's actual till (GAAP or Pilot
today, others later) needs.

```
Uber Eats ─┐
Direct Dine ┼─▶ Order Harmony ─▶ [ US: the Gateway ] ─▶ GAAP Unity (till A)
Others ────┘                                          └▶ Pilot Live (till B)
                    ◀── order status / stock / store status webhooks ──
```

Ownership split (confirmed with stakeholder):
- **Order Harmony**: owned by a different company. We are a certified "partner"
  against their spec.
- **The gateway**: owned by us.
- **POS relationships & API keys** (GAAP, Pilot): owned by us, not by Order
  Harmony and not necessarily by the merchant.

This means the "tenant" in this system is a **restaurant location**, not a
platform account — we will onboard many locations, across many merchants,
each bound to exactly one POS. There's no need for self-service tenant
onboarding UX yet, but the data model should not assume a single merchant.

## 2. The three contracts, and why they don't line up

This is the most important thing to understand before writing a line of code.
Order Harmony's spec assumes every POS partner looks the same. GAAP and Pilot
are nothing alike.

| Capability | Order Harmony expects | GAAP Unity Data-API | Pilot Live OpenAPI |
|---|---|---|---|
| Order injection | `POST /orders`, idempotent, returns `pos_order_id` | `POST /sales/create` — creates an **already-closed, already-paid** sale record (`status: TENDERED`, requires `payments[]`, `closedDate`, `employeeId` up front) | `POST /OnlineOrder/Create` — a live order with `orderStatus`, items, payments, and a **per-order `callbackUrl`** |
| Order status push-back | Required: `accepted → preparing → ready → completed`/`cancelled`, webhook to us | **None.** No webhook, no status field beyond `TENDERED`/`CANCELED`. Once injected, GAAP gives us nothing further. | Supported in principle — POS can call the `callbackUrl` we supply with `orderStatus.statusCode`/`description`. Exact status-code table isn't in the Swagger; must be confirmed with Pilot. |
| Idempotency | `Idempotency-Key` header, 24h dedupe | `externalTransactionId` (UUIDv4), 409 on replay | Not explicit in the spec — needs confirmation, likely `orderId`/`orderReference` |
| Menu pull | `GET /menu` with stable `external_id`s, min/max select, tax basis points | `GET /products` + `GET /groups`, paginated (`limit`/`skip`), no explicit modifier/min-max concept found | `GET /SalesProducts/Menu` — "products laid out per screen" |
| Health probe | `GET /health` | No dedicated endpoint — cheapest option is a bounded `GET /nodes` or `GET /locations` | `GET /api/Health/Check` |
| Auth | Bearer per-location key we issue to the merchant | `apikey` query-string param, scoped to **one whole GAAP Unity instance** (i.e. one merchant account, not per-location) | Vendor-level global API key → short-lived JWT via `POST /Authorization/Token`, scoped to `VendorId`/`StoreId` |
| Stock / 86 | Bidirectional webhooks (`item.availability_changed`) | Nothing found — polling `/products` is the only lever | Nothing found in this spec surface |

**The consequence that drives the whole design:** GAAP is a **batch/financial
recording API**, not an order-management API. It has no notion of "the
kitchen accepted this" or "it's ready" — you hand it a completed, paid sale
for accounting/stock purposes only. Pilot is much closer to what Order
Harmony expects — a live order with a feedback channel — but the details of
that feedback channel (status codes, retry behaviour, idempotency) aren't
fully documented and need confirming with Pilot support before certification.

This asymmetry has to be a first-class concept in the code, not something
papered over with `if (posType == Gaap)` scattered through the order
pipeline. See §4.

## 3. Architectural style: ports & adapters (hexagonal)

```
                         ┌─────────────────────────────┐
   Order Harmony  ─────▶ │   Inbound: Channel API       │
   (calls us)            │   POST /orders  GET /menu    │
                         │   GET /health                │
                         └───────────────┬───────────────┘
                                          │
                         ┌───────────────▼───────────────┐
                         │        Application layer        │
                         │  OrderInjectionUseCase           │
                         │  StatusSyncUseCase                │
                         │  MenuSyncUseCase                   │
                         │  HealthCheckUseCase                 │
                         └───────┬───────────────┬───────────┘
                                 │               │
                    ┌────────────▼───┐   ┌───────▼────────────┐
                    │ IPosOrderAdapter│   │ IChannelGateway     │
                    │ IPosMenuAdapter │   │ (signed webhooks     │
                    │ IPosHealthAdapter│  │  back to Order       │
                    │ IPosStockAdapter │  │  Harmony)             │
                    └───┬─────────┬───┘   └───────────────────┘
                        │         │
              ┌─────────▼──┐  ┌───▼─────────┐
              │ GaapAdapter │  │ PilotAdapter │
              └─────────────┘  └──────────────┘
```

Core domain (canonical order/menu/status model + the order status state
machine) knows nothing about GAAP, Pilot, or Order Harmony's HTTP shapes.
Each adapter implements only the ports its POS actually supports, and
declares its own **capability flags**:

```csharp
interface IPosCapabilities
{
    bool SupportsRealtimeOrderStatus;   // Pilot: true (pending confirmation). GAAP: false
    bool RequiresPrepaidClosedSale;     // GAAP: true. Pilot: false
    bool SupportsInboundStockWrite;     // both: unconfirmed, assume false until proven
    bool SupportsMenuPull;              // both: true
}
```

The application layer reads capabilities and adapts behaviour — e.g. for a
`RequiresPrepaidClosedSale` POS, the **gateway itself** owns the status state
machine after injection (see §5), instead of waiting on a webhook that will
never come.

## 4. Canonical domain model

Modelled directly on Order Harmony's contract since it's the strictest/most
complete of the three, with POS-specific fields carried as adapter-owned
metadata rather than polluting the core model.

- `CanonicalOrder` — `orderRef`, `displayId`, `sourceChannel`, `brandName`,
  `locationId`, `fulfillmentType`, `placedAt`, `scheduledFor`, `customer`,
  `deliveryAddress`, `items[]`, money fields (**all integer minor units**),
  `currency`, `prepaid`, `notes`.
- `CanonicalOrderItem` — `externalProductId`, `name`, `quantity`,
  `unitPriceCents`, `totalPriceCents`, `notes`, `modifiers[]`.
  `externalProductId` **is the POS's own native product id** (GAAP
  `productId`, Pilot `plu`) passed straight through — see the passthrough
  note in §7. No internally-invented ID scheme sits between them.
- `CanonicalModifier` — `externalModifierId`, `groupExternalId`, `name`,
  `quantity`, `priceDeltaCents`. Same passthrough rule applies.
- `OrderStatus` enum — exactly the five Order Harmony states:
  `Accepted, Preparing, Ready, Completed, Cancelled` (+ `CancelReason`).
  **This enum is the only status vocabulary that exists anywhere in the
  system.** Adapters translate into it; nothing upstream of it invents new
  states.
- `CanonicalMenu` — `Category → Product → ModifierGroup(min/max) → Modifier`,
  `externalId` stable across pulls, `taxRateBp`.

## 5. Handling the GAAP gap: synthetic status

Because GAAP gives us no feedback after `POST /sales/create` succeeds, the
gateway can't honestly report `preparing`/`ready` for GAAP-backed locations —
it doesn't know either of those things happened. Two options, and this is a
business call, not just a technical one:

1. **Synthesize a status curve.** On successful injection, immediately emit
   `accepted`, then emit `completed` after a configurable delay (e.g. average
   prep time for that location/menu). Order Harmony's tablet/KDS UI will show
   a plausible progression even though it's not driven by the actual kitchen.
2. **Collapse to two states.** Emit `accepted` on injection and `completed`
   only once we have *some* independent signal (e.g. a scheduled poll of
   `GET /sales` confirming the invoice is `TENDERED` and not `CANCELED`, or a
   fixed delay). Simpler, less honest-looking but doesn't fake intermediate
   states.

Recommendation: start with option 2 (poll-confirmed two-state) for
correctness, and treat option 1 as a UX enhancement once we have real
prep-time data per location. Either way, **this must be flagged to GAAP** —
ask whether their platform has a separate real-time channel (kitchen
display / local till webhook) we haven't seen in this Data-API, since a pure
BI/accounting API being the only injection path for live orders is unusual.

This logic lives entirely inside `GaapStatusSynthesizer`, invoked by the
application layer only when `RequiresPrepaidClosedSale` is true — it's not a
special case in the order pipeline itself.

## 6. Idempotency & retries

- **Inbound** (Order Harmony → us): dedupe on `Idempotency-Key`, minimum 24h
  window, replay the original `200`/`201` response verbatim rather than
  re-processing. Order Harmony retries up to 5 times over ~5 minutes on
  retryable failures, then flags for operator attention and may auto-pause
  the location — so our `retryable: true/false` classification on every error
  response matters operationally, not just semantically.
- **Outbound** (us → Order Harmony webhooks): stable `event_id` per event,
  retry on non-2xx with backoff `1s, 5s, 30s, 2m, 10m`, never regress a
  terminal status, include `event_time` since ordering isn't guaranteed.
- **Outbound** (us → GAAP): dedupe on `externalTransactionId` (UUIDv4 we
  generate from `order_ref`, deterministically, so retries of our own call
  are naturally idempotent); 409 is treated as success if it's *our* prior
  attempt.
- **Outbound** (us → Pilot): no documented idempotency key — treat
  `orderReference` as the dedupe key on our side until Pilot confirms
  server-side behaviour; open question for certification.

## 7. Data model (persistence)

Relational store (Azure SQL or Postgres Flexible Server — either is fine,
pick based on team familiarity). `Location` from earlier drafts of this doc
is renamed `Store` throughout — same concept, matching how the business
actually talks about it.

- `Group` — optional ownership grouping above `Store` (a franchise or
  multi-site owner). Purely organisational: consolidated reporting/billing
  rollups and shared config defaults a `Store` can inherit and override. Not
  required — a `Store` can stand alone with no `Group`.
- `Store` — belongs to an optional `Group`. Holds one `ChannelConnection`
  (inbound), one `PosConnection` (outbound), one active `BillingPlan`, a
  `timezone` (IANA id, e.g. `Africa/Johannesburg`), and a `state` (see
  "Store lifecycle" below). Onboarding a store is: create it → optionally
  attach to a `Group` → configure both connections → set a billing plan →
  activate. Each connection type maps directly onto an adapter from §3 —
  onboarding is really just picking which `IChannelGateway` and which
  `IPosOrderAdapter` implementation a store is wired to.
- `ChannelConnection` — `channel_type` (`OrderHarmony`, ...), the location
  key we issued, and any per-channel config. Modelled generically so a
  second channel isn't a schema change.
- `PosConnection` — `pos_type` (`Gaap`/`Pilot`, ...), POS-specific
  identifiers (GAAP `nodeId`/`locationId`, Pilot `vendorId`/`siteId`), and a
  Key Vault secret reference — never the raw credential.
- `BillingPlan` — `plan_type` (`Flat` | `PerTransaction`) and the applicable
  rate. Modelled as an append-only rate history (`effective_from`/
  `effective_to` per rate row), not a single mutable field — a rate change
  must never retroactively alter what an already-issued invoice was based
  on. Open question: is the gateway the system of record for billing
  computation itself, or just the source of usage data for a separate
  invoicing process? Worth deciding explicitly — see §10.
- No persisted product-mapping table. The gateway is a passthrough for
  product/modifier identifiers: `GET /menu` reshapes the POS catalogue into
  Order Harmony's category/product/modifier-group tree, but
  `external_product_id`/`external_modifier_id` **are** the POS's own native
  ids (GAAP `productId`, Pilot `plu`), not a translated internal id. Order
  injection reverses the same passthrough — no lookup required either way.
  Open question flagged against GAAP in §10: their `/products`/`/groups`
  endpoints don't expose a modifier concept even though `NewSalePayload`
  items carry an `addOns` field — worth confirming whether a GAAP-backed
  store can publish a proper modifier menu at all, since passthrough alone
  doesn't solve a capability GAAP doesn't expose.
- `Order` — canonical order snapshot, `order_ref` (unique), `pos_order_id`,
  current `OrderStatus`, timestamps in UTC.
- `OrderEvent` — append-only status history; source of truth for the
  outbound-webhook dedupe/replay logic, for audit, and for the live/
  historical observability views in §12.
- `IdempotencyRecord` — `Idempotency-Key` → cached response, TTL ≥ 24h.

### Timezones

Order Harmony sends UTC ISO-8601 with a `Z`. GAAP and Pilot both expect
naive local datetime strings with no timezone marker at all (GAAP:
`"2024-08-01T10:00:00.000"`, Pilot: `"2022-02-02 20:19:05.000"`). Rule: the
gateway stores and processes every timestamp internally as UTC, full stop,
and converts to a store's local naive time **only** at the adapter boundary
when constructing the outbound POS payload, using `Store.timezone`. Nothing
upstream of the adapter ever sees or reasons about local time — this
prevents `scheduled_for` orders firing at the wrong hour and keeps
historical reporting (§12) comparable across stores in different timezones,
should that ever matter.

### Store lifecycle

Every use case in §3 gates on `Store.state` before doing anything — a
document doesn't get to flow through the gateway just because it arrived;
the store has to be in a state that allows it.

- **Draft** — being onboarded (connections/billing not yet fully
  configured). No inbound traffic is processed even if it somehow arrives.
- **Active** — normal operation, all use cases process.
- **Paused** — order injection is rejected outright (a clear, non-retryable
  response to Order Harmony, consistent with their own auto-pause
  behaviour in §6); menu/health calls may still be served depending on
  cause. Entered automatically after repeated adapter failures (§12) or
  manually (e.g. a merchant asks to pause for maintenance). **Never
  auto-resumes** — returning to Active is always an explicit admin action,
  to avoid flapping in and out of Paused on a flaky connection.
- **Deactivated** — terminal. Connections are revoked and credentials
  rotated out; historical orders and billing history are retained for
  reporting, but no new activity is possible. Not reversible — reactivating
  a deactivated store means re-onboarding it as if new.

This is the same state a portal admin sees and acts on — see
`UI-ARCHITECTURE.md`.

## 8. Azure deployment shape

- **Compute — split by workload, not one hosting model for everything**:
  - **Inbound API** (Order Harmony calls us): **Azure App Service** (native
    .NET, not "Web App for Containers"), minimum instance count 1. No
    Docker/registry to manage — deploy is a straight `dotnet publish`.
    Deployment slots (stage, warm up, swap) give a zero-downtime deploy path
    without needing Container Apps' revision model. Order Harmony's spec has
    hard timeouts (10s for injection, 30s for menu pull) and an unforgiving
    certification checklist — a cold start on the very first request after
    idle isn't worth risking, so this path stays always-warm rather than
    scale-to-zero, and autoscale rules (CPU/request count) cover horizontal
    growth as store count and traffic increase. Container Apps was
    considered here but doesn't earn its extra operational surface (image
    builds, registry, revision config) once the worker below moved to
    Functions — the KEDA/queue-scaling case for it no longer applies to this
    tier, and App Service's slots cover the safe-deploy need just as well.
  - **Async worker** (everything reading off Service Bus — webhook
    delivery/retry, GAAP status synthesis, scheduled menu re-pull): **Azure
    Functions**, isolated worker model, **Flex Consumption plan**, Service
    Bus trigger. At zero customers this costs next to nothing beyond
    Service Bus's own base fee, and scales with volume automatically. More
    importantly for reliability: the platform owns
    complete/abandon/dead-letter/retry and drain-on-deploy for the trigger,
    which is exactly the class of bug §14 is otherwise asking a hand-rolled
    `BackgroundService` to get right. Flex Consumption specifically (not
    classic Consumption) because it supports VNet integration — needed for
    the static egress IP below, since the worker is what actually calls out
    to GAAP/Pilot/Order Harmony's webhook endpoint.
  - Neither model is per-store — see §15.
- **Static egress IP**: VNet-integrate both the App Service and the Function
  App behind a NAT Gateway. Order Harmony's certification checklist
  explicitly asks for our source IP ranges so they can allow-list us.
- **Database**: Azure SQL or Postgres Flexible Server for the tables in §7.
- **Async/reliability**: Azure Service Bus (Standard tier — needed for
  sessions/topics/DLQ) queue for outbound Order Harmony webhooks (decouples
  retry/backoff from the request path) and for the GAAP status-synthesis
  timer jobs, consumed by the Functions worker above.
- **Secrets**: Key Vault for all POS credentials and the Order Harmony HMAC
  signing secret; managed identity for the compute layer, nothing in
  appsettings.
- **Observability**: Application Insights, correlated by `order_ref` end to
  end (inbound receipt → adapter call → outbound webhook), plus explicit
  dashboards for: injection success rate per POS, webhook delivery latency,
  Pilot JWT refresh failures, GAAP synthetic-completion lag.
- **Outbound throttling — keyed by POS credential, not by store.** Concrete
  example of why this matters: GAAP issues one `apikey` per whole merchant
  estate, not per store (§2). If a restaurant chain has 20 stores on GAAP,
  all 20 send orders through us using that *same* GAAP key. A rate limiter
  that counts "requests per store" never sees a problem — no single store
  looks like it's misbehaving — but the 20 stores' combined traffic during
  a dinner rush can still exceed whatever limit GAAP enforces on that one
  key, and every one of those 20 stores starts getting rejected together.
  The counter has to be keyed on the downstream credential (the specific
  GAAP `apikey` / Pilot `vendorId` in use), shared across every store that
  happens to use it, so the gateway throttles itself before GAAP or Pilot
  do it for us. Azure API Management or Front Door can sit in front of the
  inbound Channel API for basic ingress limiting, but this outbound
  per-credential throttle is a piece of adapter-layer logic, not something
  an edge gateway product provides out of the box.

## 9. Solution structure (.NET)

```
src/
  Gateway.Domain/            canonical models, OrderStatus state machine, capability model
  Gateway.Application/       use cases: OrderInjection, StatusSync, MenuSync, HealthCheck
  Gateway.Adapters.OrderHarmony/   inbound controllers + outbound signed webhook client
  Gateway.Adapters.Gaap/     HttpClient wrapper, NewSalePayload mapping, GaapStatusSynthesizer
  Gateway.Adapters.Pilot/    JWT token cache, OnlineOrderRequest mapping, callback receiver
  Gateway.Infrastructure/    EF Core, Key Vault client, Service Bus
  Gateway.Api/               ASP.NET Core host (App Service), DI composition
  Gateway.Worker/            Azure Functions app (isolated worker, Flex Consumption) —
                             Service Bus-triggered functions for webhook retry,
                             GAAP status timers, scheduled menu re-pull
tests/
  Gateway.Domain.Tests/
  Gateway.Application.Tests/
  Gateway.Adapters.Gaap.Tests/
  Gateway.Adapters.Pilot.Tests/
  Gateway.Api.CertificationTests/   one test per row of the Order Harmony §5 checklist below
docs/
  architecture/ARCHITECTURE.md      this document
  architecture/UI-ARCHITECTURE.md   portal/frontend design — separate working thread
  reference/gaap.swagger.json       downloaded from https://data-api.gaapunity.app/swagger.json
  reference/pilot.swagger.json      downloaded from https://openapi.pilotlive.co.za/swagger/v1/swagger.json
```

The `Gateway.Api.CertificationTests` project should have one test per
scenario in the Order Harmony sandbox certification checklist (doc 04, §5) —
happy-path delivery, pickup, scheduled order, duplicate injection, unknown
PLU, modifier violation, store closed, till offline/retry, full status
round-trip, cancellation, menu pull, 86 propagation within 30s, bad webhook
signature, multi-brand ticket. Production keys aren't issued until all of
these pass, so treat this test suite as the actual Definition of Done for
each POS integration, not an afterthought.

## 10. Open questions to resolve before/while building

**With GAAP:**
1. Is a real-time order/kitchen status channel available anywhere in their
   platform, or is this Data-API genuinely the only injection path? (Drives
   whether §5's synthetic-status approach is a permanent design or a stopgap.)
2. What are production rate limits / pagination limits — sandbox caps
   `limit` at 5 "for tryout purposes."
3. How do we obtain and keep in sync: `nodeId`, `locationId`, `employeeId`
   (which user do we post sales as?), and `paymentMethodId` (which payment
   method represents "already paid via online channel")? These are required
   fields on every `NewSalePayload` and need a real mapping, not a guess.
4. Does the apikey scope correspond to one merchant's whole GAAP estate, or
   can/should we get one per location?
5. `/products` and `/groups` don't expose a modifier/add-on concept, even
   though `NewSalePayload` items carry an `addOns` field — can a GAAP-backed
   store actually publish a proper modifier menu (with min/max rules) to
   Order Harmony, or does this API only support modifiers being submitted
   blind on an order with no way to advertise what's available? (§7)

**With Pilot:**
1. The `Orderstatus.statusCode` integer enum isn't in the Swagger — need the
   actual code table (what does 2 = "Pending" map to across the full set?).
2. What does Pilot POST to our `callbackUrl` — same `OnlineOrderRequest`
   shape with updated `orderStatus`, or something else? Not in this spec.
3. Idempotency behaviour on `POST /OnlineOrder/Create` replay — undocumented.
4. `vendorId` assignment/registration process (`Authorization/Register` /
   `RegisterList` endpoints exist but the process isn't in the spec).

**With Order Harmony:**
1. Sandbox base URL, and the flow for issuing per-location keys to merchants.
2. Webhook signing-secret exchange method and their webhook source IPs (if
   any allow-listing is expected on our side).
3. Confirm our declared per-location/per-vendor rate limits before go-live.

**Internal:**
1. Is the gateway the system of record for billing computation, or just the
   usage-data source for a separate invoicing process? (§7)

**Explicit non-goals for v1 (decided, not just deferred):**
- **Order modification after injection** — no refund, void, or amend
  capability. None of the three specs reviewed expose such an endpoint, and
  the canonical `OrderStatus` enum has no state for it. Confirmed
  2026-08-20 that this isn't required at this stage. If it's needed later,
  it's a new canonical status plus new adapter capability flags, not a
  small addition — worth remembering it's a real design change, not a
  checkbox, whenever it does come up.

## 11. Suggested delivery phases

1. **Skeleton + one adapter, one path.** Domain model, state machine,
   `Gateway.Api` implementing `POST /orders` + `GET /health` for Order
   Harmony, Pilot adapter only (it's the closer match to the target
   contract), no persistence beyond in-memory — prove the shape end to end
   in sandbox.
2. **Persistence + idempotency + outbound webhooks.** Real database, signed
   webhook delivery with retry/backoff, `GET /menu` for Pilot.
3. **GAAP adapter + synthetic status.** Second adapter, proving the
   capability-flag design actually isolates the difference cleanly.
4. **Stock/86, store status/hours, certification test suite.** Work through
   the full checklist in doc 04 for both POS platforms.
5. **Hardening.** Rate limiting, dashboards/alerting, location auto-pause
   parity with Order Harmony's own behaviour, key rotation runbook.

## 12. Observability — live and historical

Two different consumers read off the same event stream, so there's no need
to build two systems:

- Every meaningful step (order received, adapter call, status transition,
  webhook delivered/retried, store paused) becomes an `OrderEvent` row (§7)
  and an OpenTelemetry span/event tagged with `store_id`, `order_ref`,
  `pos_type`, `channel_type`, `outcome`, `duration_ms`.
- Ship that telemetry to Application Insights — distributed tracing (follow
  one order end-to-end through inbound → adapter → outbound webhook), a live
  metrics stream, and KQL for ad-hoc historical querying come largely for
  free rather than needing to be built.
- For the portal's own live view — this is for store owners/ops staff, not
  just engineers watching logs — push `OrderEvent` writes to connected
  sessions via a Service Bus topic that the portal's single instance
  subscribes to and fans out in-process to Blazor circuits. That's a
  genuinely live feed without polling, and without provisioning a
  dedicated backplane (Azure SignalR Service) before it's actually needed
  — see `UI-ARCHITECTURE.md` for when that changes.
- For historical charts at scale, don't query raw `OrderEvent` rows once
  there are thousands of stores — pre-aggregate into hourly/daily rollups
  per store (volume, success rate, latency percentiles) on a schedule, and
  read those for dashboards. Keep raw events for drilling into one specific
  order, not for summary charts.
- Alerting mirrors what Order Harmony already does on their side: repeated
  failures at a store should flag it and, past a threshold, auto-pause it —
  surfaced in the portal itself, not just in a log. This is part of
  observability, not a separate system: the same alerting rule that flags a
  store in the portal also fires a **push notification** (Teams webhook to
  start, email as a fallback) so it reaches someone even when nobody is
  looking at the dashboard — with a 3-person team, "surfaced in the portal"
  alone isn't enough to guarantee it gets seen in time.

## 13. Security & access

Internal-only changes the calculus: no public-facing auth story needed, just
a good internal one.

- **Microsoft Entra ID** as the identity provider for the portal, via
  `Microsoft.Identity.Web`. This is the existing Microsoft/Azure stack, so
  it gets Microsoft Authenticator MFA, Conditional Access (require MFA,
  block non-corporate devices, require a compliant device), and audit
  logging without building any of it.
- Authorization via Entra App Roles or security groups (e.g.
  `GatewayAdmin`, `GatewayViewer`) rather than a bespoke permissions system —
  no product reason yet to build one.
- Independent of portal login: GAAP/Pilot API keys and the Order Harmony
  signing secret live in Key Vault behind managed identity regardless of who
  is logged into the portal.
- If external access is ever needed later (a specific merchant, a POS
  partner contact), Entra B2B guest access covers it without redesigning the
  auth model. Nothing needs building for that now.

## 14. Messaging reliability — no lost signals across a deploy

Two guarantees stacked together, both achievable with Azure Service Bus:

- **At-least-once delivery, not exactly-once.** Service Bus's PeekLock
  semantics mean a message only leaves the queue once the handler explicitly
  completes it. If the process is killed mid-processing — including by a
  deployment — the lock expires and the message becomes visible again for
  another instance to pick up. Nothing is lost; the tradeoff is that a
  message can occasionally be delivered twice, which is why every handler
  needs to be idempotent — already required anyway for the Order
  Harmony/GAAP dedupe logic in §6.
- **Graceful shutdown on deploy.** A rolling deployment must stop pulling
  new messages, let in-flight ones finish (or let their lock lapse cleanly),
  then exit. Running the worker as an Azure Functions app with a Service Bus
  trigger (§8) means the platform's drain-on-deploy behaviour handles this
  rather than a hand-rolled `BackgroundService.StopAsync` — one less place
  to get graceful shutdown subtly wrong.
- **Outbox pattern on the write side.** Whenever a step needs to both write
  to the database and enqueue a message (e.g. "order recorded" + "send
  status webhook"), do both in one DB transaction by writing the outbound
  message into an `Outbox` table first, then have a separate dispatcher
  publish from the outbox to Service Bus. This closes the one gap
  at-least-once delivery doesn't cover on its own: a DB commit succeeding
  while the message publish fails.
- **Dead-letter queue with alerting**, so a genuinely poisoned message
  surfaces as an incident instead of retrying silently forever or vanishing.
- **Session-enabled queues, `SessionId = store_id`, on every queue carrying
  store-scoped commands** (outbound webhook queue, GAAP status-synthesis
  timer queue). Without this, two messages for the same store — say, two
  status updates in quick succession, or a retry racing a fresh event — can
  be picked up by two different worker instances and processed out of
  order or concurrently, which risks a double-write against a single
  physical till that can't handle two simultaneous writes cleanly. Service
  Bus sessions guarantee messages sharing a `SessionId` are handled in
  order by exactly one consumer at a time; the Functions Service Bus
  trigger supports session-enabled queues natively, with a configurable
  max-concurrent-sessions setting so this doesn't become a
  one-store-at-a-time bottleneck across the whole fleet.

Put together — PeekLock, idempotent handlers, the outbox pattern, graceful
shutdown, DLQ alerting, and per-store sessions — this is as close to "never
lose a signal, never process one out of order" as a distributed system
honestly gets.

## 15. Scaling to 1,000s of stores

A store is a row of configuration, not a deployable unit: there's no need
for (and real cost to) an instance per store, container or otherwise. What
scales is one shared fleet growing horizontally with aggregate load,
differentiated purely by `store_id` in the data, never in the deployment
topology. That principle holds regardless of which hosting model runs the
fleet — it's why the App Service-vs-Container-Apps choice in §8 doesn't
trade away anything on the scaling front.

- **App Service** for the inbound API (§8), min 1 instance for predictable
  latency, autoscale rules (CPU/request count) scaling out horizontally as
  request volume grows. If this ever genuinely outgrows App Service's
  model — needing Dapr, fine-grained canary traffic-splitting, or
  Kubernetes-compatible tooling — Container Apps or AKS are both still
  available migration paths, but that's a future decision, not today's.
- **Azure Functions (Flex Consumption)** for the async worker (§8) — no
  containers to manage at all here, and it scales with Service Bus queue
  depth automatically. At zero customers this is close to free; at 1,000s of
  stores it scales without any capacity planning on our part.
- The database and Service Bus are what actually need attention at
  1,000s-of-stores scale: index everything on `store_id`, keep the
  observability rollups (§12) off the transactional write path, and
  consider Service Bus Premium if throughput demands it. Scaling out the
  compute layer doesn't help if the database underneath is the constraint.

## 16. Data protection & POPIA

§13 covers who can log into the portal; this is about the customer data
that flows through it regardless of who's logged in. `CanonicalOrder`
carries `customer.name`, `customer.phone`, `customer.email`, and
`delivery_address` (§4) — personal information under POPIA, and South
Africa is the operating jurisdiction, so this needs an actual plan rather
than an assumption that portal security covers it.

- **Data residency.** Host in **Azure South Africa North** (Johannesburg),
  not a default region picked for other reasons — keeps this data resident
  locally rather than raising a cross-border transfer question that didn't
  need to exist.
- **Minimisation.** Only store what's operationally necessary. Where a
  channel already masks contact details (Order Harmony's spec allows
  `customer.phone` to be "a masked relay number"), prefer the masked form
  over asking for or retaining a raw number we don't need.
- **Retention.** Raw customer PII (name, phone, email, address) on `Order`
  is retained for a fixed window tied to operational need — proposed
  default **90 days**, covering dispute/chargeback and billing
  reconciliation — after which those fields are anonymised in place
  (replaced with a redacted placeholder) while order totals, timestamps,
  and status history in `OrderEvent` are retained indefinitely for
  reporting. This needs sign-off as a business decision, not just a
  technical default — flagging 90 days as a starting proposal, not a
  final answer.
- **Encryption at rest.** Confirm explicitly rather than assume: Azure SQL
  and Postgres Flexible Server both encrypt at rest by default, but this
  should be a checked item at deployment time, not an assumption baked
  into this document.
- **Access control & audit.** Raw customer PII fields should be visible
  only to authorised portal roles (§13), and viewing a specific order's
  customer details should be audit-logged — who looked at what, and when.
- **Data-subject requests.** The direct customer relationship belongs to
  the channel/merchant, not us, but a deletion or access request could
  still be forwarded to us since we do store the data. Needs at minimum a
  manual admin capability to locate and purge/anonymise a specific
  customer's historical orders (by `order_ref`, phone, or email) on
  request — doesn't need to be self-service initially, but it needs to
  exist.
- **Downstream processors.** GAAP, Pilot, and Order Harmony all process
  this same customer data further downstream. That's a contractual/legal
  question about data processing agreements with those parties, not a
  technical one — flagging it here so it doesn't fall through the crack
  between "that's legal's problem" and "that's engineering's problem."
