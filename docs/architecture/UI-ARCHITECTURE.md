# Portal UI — Architecture (working thread)

Split out from `ARCHITECTURE.md` because it was being treated as an
afterthought to the backend — it isn't. This doc is where the actual
frontend/portal decisions get made. Starting point, not a finished design.

## What's already decided elsewhere (don't re-litigate here)

- Auth: Microsoft Entra ID, internal-only, Authenticator MFA via
  Conditional Access — `ARCHITECTURE.md` §13.
- The event stream the portal reads from: `OrderEvent` rows +
  OpenTelemetry spans shipped to Application Insights — `ARCHITECTURE.md`
  §12.
- Store lifecycle states the UI needs to expose and let admins act on —
  `ARCHITECTURE.md` §7 (Store lifecycle).

## Decided

- **Rendering model: Blazor Server.** Every interaction (not just the live
  feed) round-trips over a persistent connection to the server —
  acceptable here because this is a 3-person internal tool on stable
  connections, not a public product exposed to arbitrary networks.
  Practical consequences worth remembering, not reasons to reverse the
  decision: a dropped connection shows a "reconnecting…" banner and
  self-heals (nothing already saved is lost, unsubmitted form input can
  be); a deploy disconnects every active user at once, so deploys should
  avoid the middle of someone's onboarding flow, same as any web app; a
  corporate proxy that kills idle connections aggressively would show up
  as frequent reconnects — not expected here, but worth knowing the
  symptom if it ever happens.

- **Separate host: `Gateway.Portal`, its own App Service Plan** (not just
  a separate site on a shared plan — genuine resource isolation, not just
  process separation, is the whole point). Deployment picture:
  - `gateway-api` — own Plan, VNet-integrated behind the NAT Gateway for
    the static egress IP Order Harmony requires.
  - `gateway-portal` — own Plan, own Entra ID app registration for
    interactive sign-in, entirely separate from `gateway-api`'s
    Bearer-location-key auth.
  - Both, plus the `Gateway.Worker` Function App, share **one VNet**, each
    in its own subnet, reaching the database over a private endpoint
    rather than the public internet — consistent with the POPIA plan in
    §16.
  - The onboarding wizard's "test connection" step (below) means the
    portal also makes outbound calls to GAAP/Pilot directly, not just
    reads/writes the database — a new outbound path worth remembering
    when thinking about network egress, even though neither POS has
    asked for IP allow-listing the way Order Harmony has.
  - One repo/solution, two independent deployment pipelines, each with
    its own staging slot for zero-downtime swaps.

- **Data access: direct**, via the same `Gateway.Infrastructure` EF Core
  context `Gateway.Api` uses, referenced as a shared project. No internal
  API layer between the portal and its own database — there's no second
  team or trust boundary here to justify one.

- **Live updates: skip Azure SignalR Service for now.** It's a backplane
  that earns its cost once either something outside the portal process
  needs to push into it, or the portal itself runs on more than one
  instance. The first is already true (`Gateway.Api`/`Gateway.Worker` are
  separate processes), but the second isn't — no reason for a 3-person
  internal tool to run the portal on multiple instances for a long while.
  Instead: `Gateway.Api`/`Gateway.Worker` publish a lightweight
  notification onto a Service Bus topic (already provisioned, no new
  resource), and the portal runs a single background subscriber that fans
  out in-process to connected Blazor circuits. Revisit Azure SignalR
  Service specifically if/when the portal ever needs multiple instances —
  not before. (This walks back `ARCHITECTURE.md` §12's original wording,
  which named SignalR Service outright.)

- **Component/UI library: MudBlazor**, confirmed 2026-08-20. Larger
  community footprint and more API stability across versions than Fluent
  UI Blazor, and more mature data-grid/charting components — matters
  directly for the command centre below (fleet status grid, live ticker).
  Traded away the Entra/Microsoft 365 visual-consistency argument for
  Fluent UI Blazor deliberately; theme MudBlazor's palette to fit rather
  than rely on it looking Microsoft-native out of the box.

- **The home screen is a command centre, not a dashboard of tiles.** A
  command centre for an online ordering system reads like a NOC board —
  exception-first, not volume-first. The job is "show me what needs a
  human right now," not "show me numbers." Concretely:
  - A **fleet status grid** — every store as a tile colored by state
    (green Active, amber Paused/degraded, grey Draft) — scanning the
    screen shows fleet health before clicking anything.
  - A **live scrolling ticker** of events (orders, retries, failures),
    most recent first, color-coded by outcome.
  - **Exceptions sorted to the top, always** — a Paused store or a
    stuck-retrying order is the first thing visible; healthy operation
    recedes into the background.
  - **Rolling headline KPIs with trend**, not point-in-time counts —
    orders today with a trend arrow, success rate over the last hour.
  - This is core product, built early — it's what "live observability"
    meant from the start of this project, not an afterthought behind
    Azure Monitor. Deep ad-hoc historical analysis (long-range trends,
    arbitrary KQL) still links out to Azure Monitor Workbooks — a
    genuinely different, exploratory use case Application Insights
    already does well.

- **Onboarding wizard includes a live "test connection" step** before a
  store can move Draft → Active, calling the POS/channel health adapters
  that already exist per §3. Cheap, and it's exactly what prevents a
  broken store going live.

## Page/feature inventory

- **Command centre (home screen)** — fleet status grid, live scrolling
  event ticker, exceptions surfaced first, rolling headline KPIs. See
  "Decided" above. This is the flagship screen, not a secondary one.
- Store list — the administrative view (search/manage stores), distinct
  from the command centre's operational fleet grid, with state
  (Draft/Active/Paused/Deactivated) visible as a status chip.
- Store onboarding wizard: create → attach to Group (optional) → configure
  ChannelConnection → configure PosConnection → set BillingPlan → test
  connection → activate.
- Store drill-down — click a store on the command centre grid or list to
  see its recent order timeline.
- Order drill-down — one order's full trace (inbound payload → adapter
  call → outbound webhook), matching the Application Insights distributed
  trace by `order_ref`.
- Historical analysis — long-range trends beyond "today/this week," ad-hoc
  querying — linked out to Azure Monitor Workbooks rather than rebuilt
  in-portal.
- Admin/role management — who can see what; flat `GatewayAdmin` /
  `GatewayViewer` roles for now per §13, revisit if per-store/per-group
  scoped visibility is ever needed (e.g. a franchise Group owner who
  should only see their own stores).

**Scope for the Pilot POC (Phase 4) specifically:** the command centre and
onboarding wizard are the hard requirements to get a real store through
Phase 4 — the command centre *is* the live feed that was asked for, not a
separate thing to build alongside it. Store/order drill-down can follow
close behind; historical Workbooks integration and admin role management
can wait for Phase 6 hardening.
