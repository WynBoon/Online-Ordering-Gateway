# Solution Scaffold — What Was Built

> **Historical snapshot (2026-08-20).** This records the first scaffold pass.
> For current layout, runbooks, and behaviour see `docs/LOCAL-DEV.md`,
> `docs/user-manuals/`, and `docs/architecture/ARCHITECTURE.md`. Project
> counts, test counts, and “what’s not done” below are frozen as of that
> date and will drift.

**Date:** 2026-08-20
**Scope:** Full .NET solution scaffold, generated in one pass from the design
already agreed in `architecture/ARCHITECTURE.md` and `architecture/UI-ARCHITECTURE.md`.

## 1. What exists

At scaffold time: **14** projects under `Gateway.slnx` (a `.slnx` file, not
`.sln` — the .NET 10 SDK's default solution format). All targeted **.NET 8**
because the Azure Functions Worker SDK only listed .NET 10 as Preview.

**Since then** the solution also includes `Ordering.App` / `Ordering.Core`
(Harmony stand-in), `StoreDevice.App` / `StoreDevice.Core` (in-store tablet),
and `Gateway.Infrastructure.Tests`. Run `dotnet test Gateway.slnx` for a
live test count (certification scenarios remain `[Fact(Skip=…)]`).

Scaffold tree (2026-08-20):

```
src/
  Gateway.Domain/                  Canonical models, OrderStatus state machine, capability model
  Gateway.Application/             Ports (interfaces), use cases, repository interfaces
  Gateway.Adapters.OrderHarmony/   Inbound controllers (POST /orders, GET /menu, GET /health),
                                   outbound signed webhook sender, Bearer location-key auth
  Gateway.Adapters.Gaap/           GAAP HTTP client, DTOs, order/menu/health adapters,
                                   status synthesizer (Completed/Cancelled poll backstop only)
  Gateway.Adapters.Pilot/          Pilot HTTP client + JWT cache, DTOs, order/menu/health
                                   adapters, inbound status-callback controller
  Gateway.Infrastructure/          EF Core DbContext + migrations, repositories, Key Vault
                                   secret resolver, Service Bus outbox dispatcher
  Gateway.Api/                     ASP.NET Core host (App Service) — the Order Harmony-facing surface
  Gateway.Worker/                  Azure Functions host (Flex Consumption) — webhook delivery,
                                   GAAP status polling, outbox dispatch, scheduled menu re-pull
  Gateway.Portal/                  Blazor Server + MudBlazor — the command centre, store list,
                                   onboarding wizard
tests/
  Gateway.Domain.Tests/            State machine tests
  Gateway.Application.Tests/       Use case tests (Store-state gating, adapter resolution)
  Gateway.Adapters.Gaap.Tests/     Idempotency-derivation tests
  Gateway.Adapters.Pilot.Tests/    Idempotency-derivation + status-code mapping tests
  Gateway.Api.CertificationTests/  One test per Order Harmony sandbox checklist scenario
```

Reference material already in the repo before this scaffold, used to shape
the adapter DTOs: `docs/reference/gaap.swagger.json`,
`docs/reference/pilot.swagger.json`.

## 2. Build and test results (scaffold day)

- **Full solution build: 0 errors, 0 warnings.**
- **30 real tests passed** on that day (Domain / Application / GAAP / Pilot).
  Do not treat this number as current — re-run `dotnet test Gateway.slnx`.
- **14 Order Harmony certification scenarios exist as `[Fact(Skip=...)]`
  placeholders** (`Gateway.Api.CertificationTests`), each skip reason
  pointing at what's missing (sandbox credentials, a live test store). They
  are not faked passes — filling these in is the real Definition of Done
  for Phase 3 in `docs/planning/project-plan.md`.
- **An EF Core `InitialCreate` migration was generated successfully**
  against the full data model from `ARCHITECTURE.md` §7 — the schema is
  provably buildable, not just described in prose.

## 3. What's real, not just scaffolded

- The `OrderStatusTransition` state machine (`Gateway.Domain/Orders/`) —
  enforces the fixed accepted→preparing→ready→completed sequence, allows
  cancellation from any non-terminal state, and refuses to regress a
  terminal status. Tested.
- `OrderInjectionUseCase`'s Store-state gating — a Draft, Paused, or
  Deactivated store's orders are rejected before any adapter is even
  called. Tested with real assertions, not just a happy path.
- Keyed dependency injection resolves `IPosOrderAdapter`/`IPosMenuAdapter`/
  `IPosHealthAdapter` by `PosType` at runtime
  (`serviceProvider.GetRequiredKeyedService<T>(connection.PosType)`) —
  there is no `if (posType == Gaap)` branch anywhere in the order pipeline.
- GAAP and Pilot DTOs (`Dtos/` folders in each adapter project) are typed
  directly from the downloaded swagger specs — field names, required-ness,
  and JSON property names match what those APIs actually expect, not a
  guessed shape.
- The Order Harmony webhook signature
  (`OrderHarmonySignatureService.Sign`) implements doc 02 §4's algorithm
  exactly: lowercase hex HMAC-SHA256 of `"{timestamp}.{raw_body}"`.
- The outbox pattern is real: `OrderInjectionUseCase` and `StatusSyncUseCase`
  write an `OutboxMessage` in the same call as the DB save; a separate
  `OutboxDispatcher` (invoked by a Worker timer) publishes to Service Bus.
- **Messaging design was refined during the build**: outbound events publish
  to a Service Bus **topic** (`order-events`), not a queue, with two
  subscriptions — `webhook-delivery` (session-enabled, consumed by
  `Gateway.Worker` for reliable, ordered delivery to Order Harmony) and
  `portal-live-feed` (consumed by `Gateway.Portal` for the command centre's
  live ticker). This wasn't spelled out at this level of detail in the
  architecture docs beforehand — it fell out of actually wiring the two
  independent consumers together.
- The Blazor command centre (`Gateway.Portal/Components/Pages/CommandCentre.razor`)
  is a working page, not a mockup: it queries the real repository layer for
  the fleet grid, sorts exceptions (Paused stores) to the top, and listens
  to `LiveOrderFeedService` for the live ticker.
- The store onboarding wizard (`StoreOnboardingWizard.razor`) creates a real
  `Store` + `ChannelConnection` + `PosConnection` + `BillingRate`, then
  calls the actual POS health adapter as its "test connection" step before
  allowing activation — matching the decision in `UI-ARCHITECTURE.md`.

## 4. What was deliberately stubbed, not silently guessed

Each of these has a code comment pointing at the open question it depends
on (cross-referenced to `ARCHITECTURE.md` §10):

- **GAAP `employeeId`/`paymentMethodId`/`terminalId`** — read from
  `PosConnection.ExtraConfig`, a loose config dictionary. If they're
  missing, `GaapOrderAdapter` fails loudly (`pos_config_incomplete`)
  rather than fabricating a value, since posting a sale under the wrong
  employee or payment method would be a real, hard-to-detect error.
- **GAAP product pricing** — `GaapProduct.Pricing` is left as a raw,
  unparsed `JsonElement`. Their pricing schema (cost basis, price options)
  is deeper than could be responsibly mapped without real sandbox data to
  check against.
- **GAAP menu modifiers** — omitted entirely. Their `/products`/`/groups`
  endpoints don't expose a modifier concept, even though order submission
  has an `addOns` field — this is the open question about whether a
  GAAP-backed store can publish a modifier menu at all.
- **Pilot `orderStatus.statusCode` mapping** — `PilotStatusCodeMapping`
  only maps the one confirmed value (2 = "Pending" → Accepted). Every other
  code is rejected rather than guessed, because reporting a wrong status to
  Order Harmony is worse than reporting none.
- **Pilot callback payload shape** — assumed to be the same
  `OnlineOrderRequest` shape with an updated `orderStatus`, since that's
  the working hypothesis, not a confirmed fact.
- **Pilot `paymentMethod` value** for "already paid via the online
  channel" — hardcoded to `"EFT"` as a placeholder pending Pilot's answer.
- **Command centre success-rate KPI** — renders as "—" (null) permanently
  until the hourly rollups from `ARCHITECTURE.md` §12 exist; deliberately
  not computed from raw events, which the design explicitly says not to
  query for this view.
- **Menu-change detection** — `ScheduledMenuRepullFunction` only re-fetches
  a store's menu hourly; it does not yet diff against the previous pull or
  fire a `menu.changed` webhook. That diffing logic is Phase 4 scope.

## 5. A build-environment gotcha worth knowing

`dotnet add package` without an explicit `--version` grabs the SDK's
default version — in this environment that was `10.0.11`. For most packages
(Microsoft.Extensions.*, Azure.*) that's harmless even on a net8.0 project.
It is **not** harmless for `Microsoft.EntityFrameworkCore.SqlServer` and
`Microsoft.EntityFrameworkCore.Design` — those 10.x builds only target
net10.0 and fail to restore on net8.0. Both are pinned to `8.0.11` in
`Gateway.Infrastructure.csproj`. Worth remembering if adding more EF Core
packages later.

## 6. What was not done on scaffold day

- **No Azure resources provisioned** at that time. The code is written so it
  runs locally without Key Vault / Service Bus configured. UAT App Service
  and related resources may exist now — see deploy scripts and
  `docs/LOCAL-DEV.md`.
- **Git:** this repo is under git today. The scaffold note that “no git
  repository” existed is obsolete.
- **The 14 certification tests are placeholders**, not implementations —
  see §2. That part remains true.

## 7. How this maps back to the project plan

This scaffold covers the groundwork for Phase 1 (`docs/planning/project-plan.md`)
across both the Pilot and GAAP tracks at once, since the domain model,
ports/adapters shape, and Order Harmony channel are shared by both. What's
still ahead for Phase 1 to actually close out: real Azure resources, real
sandbox credentials for all three integrations, and turning the 14 skipped
certification tests into real, passing ones against Eddie's test till and
Nick's DineDirect sandbox.
