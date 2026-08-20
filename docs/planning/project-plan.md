# Project plan — Order Gateway

## Team

| Person | Role |
|---|---|
| Wyndham | Builds the gateway (API, adapters, infrastructure). |
| Eddie | Runs the test POS (Pilot) on his machine — the de facto test customer for the POS side. |
| Nick | Owns the DineDirect/Order Harmony side — validates that the ordering-system integration behaves correctly. |

## Sequencing

**Pilot first.** Pilot confirmed the right endpoint and is fully on board. GAAP's
correct endpoint is still unconfirmed (email sent, awaiting their answer), so
GAAP runs as a **second, gated track** — its build only starts once they
confirm what the real Online Order API actually is. Nothing about the
Pilot-first work is wasted on GAAP: the canonical domain model, the Order
Harmony channel implementation, and the ports/adapters shape from Phase 0–2
are reused as-is: GAAP only needs its own adapter plugged in.

The named POC customer (restaurant site) is **still to be decided** — flagged
everywhere below as `[POC site — TBD]`. Fill this in once chosen; it doesn't
block starting Phase 0–3.

---

## Phase 0 — Foundations

**Owner:** Wyndham. **Estimate:** ~1.5 weeks.

- Solution scaffold: `Gateway.Domain`, `Gateway.Application`,
  `Gateway.Adapters.*`, `Gateway.Infrastructure`, `Gateway.Api`,
  `Gateway.Worker`.
- Canonical domain model + the five-state order status state machine.
- Core schema: `Merchant`, `Location`, `PosBinding`, `ProductMapping`,
  `Order`, `OrderEvent`, `IdempotencyRecord`.
- Azure skeleton: Container Apps, Key Vault, database, Service Bus, CI/CD
  pipeline, dev environment.

**Exit criteria:** solution builds and deploys to an Azure dev environment;
a trivial end-to-end health check passes; schema is migrated and seedable.

---

## Phase 1 — Pilot adapter

**Owner:** Wyndham (build); Eddie plugs in his test POS toward the end.
**Estimate:** ~2.5–3 weeks.

- Pilot auth: token fetch, caching, refresh before expiry.
- `OnlineOrder/Create` mapping from the canonical order.
- Callback receiver endpoint, mapping Pilot's status back into the
  five-state canonical vocabulary.
- Menu pull (`SalesProducts/Menu`) mapped into the canonical menu.
- Health check adapter (`api/Health/Check`).
- Idempotency and error-code mapping (retryable vs terminal).

**Exit criteria:** a synthetic order injects successfully into Eddie's test
till and appears correctly; a status change on the till is reflected
correctly on our side via the callback.

---

## Phase 2 — Order Harmony channel

**Owner:** Wyndham (build); Nick validates from the DineDirect sandbox
toward the end. **Estimate:** ~2 weeks.

- Inbound `POST /orders` — Bearer per-location key auth,
  `Idempotency-Key` dedupe.
- `GET /menu`, `GET /health`.
- Outbound signed webhook sender (HMAC signature, retry/backoff schedule,
  `event_id` dedupe, never regress a terminal status).

**Exit criteria:** Nick can trigger a test order from DineDirect's sandbox,
see it land correctly through the gateway, and see status webhooks arrive
back in DineDirect's system.

---

## Phase 3 — Integration & certification test

**Owner:** Eddie + Nick + Wyndham together. **Estimate:** ~1.5–2 weeks.

- Wire Eddie's test POS and Nick's DineDirect sandbox together end-to-end
  through the gateway.
- Run through Order Harmony's full sandbox certification checklist (14
  scenarios — happy path, pickup, scheduled order, duplicate injection,
  unknown PLU, modifier violation, store closed, till offline/retry, full
  status round-trip, cancellation, menu pull, 86 propagation, bad webhook
  signature, multi-brand ticket).
- Fix defects found.

**Exit criteria:** all 14 scenarios pass consistently; Eddie and Nick both
sign off from their respective sides.

---

## Phase 4 — POC with named customer

**Site:** `[POC site — TBD]`. **Estimate:** 2–4 week live window (calendar
time will likely exceed engineering effort here — this phase is about
watching real traffic, not building).

- Onboard one real, low-risk restaurant site running Pilot: issue its Order
  Harmony location key, configure its `PosBinding` and `ProductMapping` for
  real.
- Run limited live order volume under close supervision.
- Track success metrics, defined now so there's no ambiguity at review time:
  - Order accuracy — zero lost or mismatched orders.
  - Injection latency (median and p95).
  - Webhook delivery success rate to Order Harmony.
  - Zero duplicate orders/charges.
  - Rate of manual intervention needed.

**Exit criteria:** go/no-go review against the metrics above.

---

## Phase 5 — GAAP track (parallel, gated)

**Start date:** unknown — contingent on GAAP confirming what their real
Online Order API is.

Mirrors Phases 1–4, scoped to GAAP only:

- **GAAP adapter build** (~2 weeks, faster than Pilot's since the domain
  model, state machine, and Order Harmony channel are already built) —
  includes the GAAP status-synthesizer described in the architecture doc,
  since GAAP won't push status back to us the way Pilot does.
- **GAAP integration test** against the same certification checklist.
- **GAAP POC** on a GAAP-running site (same site as Phase 4 if it happens to
  run GAAP too, otherwise a separate `[POC site — TBD]`).

**Exit criteria:** same shape as Phases 3–4, scoped to GAAP.

---

## Phase 6 — Release hardening & GA

**Owner:** Wyndham. **Estimate:** ~1–2 weeks.

- Observability dashboards and alerting (injection success rate, webhook
  latency, adapter error rates).
- Rate limiting, location auto-pause parity with Order Harmony's own
  behaviour.
- Key rotation runbook.
- Onboarding runbook — how a new location gets added without engineering
  involvement for the routine case.
- Final certification sign-off with Order Harmony, production keys issued.

**Exit criteria:** production keys issued by Order Harmony; a new location
can be onboarded by following the runbook alone.

---

## Rough timeline (Pilot-only path to GA)

Phase 0 through Phase 4 plus Phase 6 is roughly **9–11 weeks of engineering
effort**, not counting however long Phase 4's live-observation window runs in
calendar time, and not counting how long GAAP takes to respond (Phase 5 is
additive and unblocked independently).

## Open items to close before/during the plan

- Name the Phase 4 POC site.
- GAAP's response on the correct Online Order API (blocks Phase 5 start).
- Pilot's outstanding items from the kickoff call (status-code table,
  provisioning process) — needed before Phase 1 can fully complete.
