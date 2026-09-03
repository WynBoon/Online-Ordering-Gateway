# Order Harmony channel

User and technical manual for the **inbound channel** Order Harmony / Dine Direct calls, and the **outbound status webhooks** we send back. This is Phase 2 of `docs/planning/project-plan.md`. The POS mapping (Pilot today) is a separate adapter behind this contract.

Related: `docs/architecture/ARCHITECTURE.md` (design), `docs/LOCAL-DEV.md` (how to run locally).

---

## 1. Who this is for

| Reader | Use this to |
|---|---|
| **Nick** (Order Harmony / Dine Direct) | Point the sandbox at our API, issue a location, inject a test order, and verify menu / health / webhooks. |
| **Wyndham** (gateway) | See what the channel actually implements, what Order Harmony must send, and what is still stubbed. |
| **Eddie** (Pilot till) | Know that a successful `POST /orders` is already a live ticket on the bound till — this channel is the front door, not a second POS. |

---

## 2. What this channel is

Order Harmony already aggregates Uber Eats, Direct Dine, and other sources. From their point of view we are **one POS partner**. We implement their partner spec:

```
Uber Eats / Direct Dine / others
        → Order Harmony
            → Gateway Channel API  (this document)
                → Pilot (or later GAAP)
                    → till
            ← signed status webhooks
```

The tenant is a **restaurant location (Store)**, not a platform account. Each store has:

- one **ChannelConnection** (inbound: location key, webhook URL, signing secret)
- one **PosConnection** (outbound: Pilot vendor/site + API key today)

Product IDs are a **passthrough**. `GET /menu` `external_id` values **are** the till PLUs. `POST /orders` must send those same IDs back as `external_product_id` / `external_modifier_id`. We do not invent a mapping table.

---

## 3. Current status (as of 2026-09-01)

Proven end-to-end against **Pilot QA** through this channel: menu pull, health (once a Pilot key is saved), and order injection (`POST /OnlineOrder/Create` returned `Status: true`, till reference issued).

| Capability | Status |
|---|---|
| `Authorization: Bearer {location_key}` | Done |
| `GET /health` | Done — pings the bound POS |
| `GET /menu` | Done — Pilot catalogue reshaped into OH category tree |
| `POST /orders` + `Idempotency-Key` | Done — injects to Pilot; **201** cached for 24h |
| Error envelope `{ code, message, retryable }` | Done |
| Store-state gate (Draft / Paused / Deactivated reject orders) | Done |
| Outbound signed status webhooks | **Code exists**; delivery needs Service Bus + Worker + a real OH webhook URL |
| Location-key rotation (24h overlap) | Data model only — no portal rotate action yet |
| 14 sandbox certification tests | Placeholders (`[Fact(Skip=…)]`) |
| Stock / 86 within 30s | Not built |
| Order amend / refund after injection | Explicit non-goal for v1 |

---

## 4. Onboarding a location

A store cannot take traffic until both connections are set and the store is **Active**.

### What we issue to Order Harmony

| Item | Where it lives | Notes |
|---|---|---|
| **Location key** | `ChannelConnection.LocationKey` | Bearer token Order Harmony pastes into their integration screen. Unique per store. |
| **Webhook URL** | Issued **by Order Harmony** per environment | We POST status events here. Stored on `ChannelConnection.WebhookUrl`. |
| **Signing secret** | Shared HMAC secret | We sign outbound webhooks; they verify. Stored as `SigningSecretRef` (local: `local://oh-signing-secret`, later Key Vault). |

### What we configure on our side (portal)

1. Create store (name, IANA timezone e.g. `Africa/Johannesburg`).
2. Channel: location key, webhook URL, signing secret.
3. POS: Pilot API key → **Test connection** (resolves `vendorId` / `siteId`) → save.
4. Activate. Draft / Paused stores reject `POST /orders` with `store_not_active` (**409**, `retryable: false`).

Local seeded store for sandbox work:

| Store | State | Bearer location key |
|---|---|---|
| Local Dev Kitchen | Active | `dev-local-location-key` |
| Local Dev Cafe | Paused | `dev-paused-location-key` |

Paused is useful: Order Harmony should see a clear non-retryable reject, consistent with their own auto-pause behaviour.

### Open with Order Harmony (needed before Nick’s sandbox sign-off)

1. Sandbox **base URL they will call** (ours) and the **webhook URL they issue** per location.
2. How location keys are entered on their side, and whether they expect us to generate them or they generate them.
3. Signing-secret exchange (we already implement HMAC as specified below).
4. Source IPs to allow-list, if they require it (production: static egress NAT — `ARCHITECTURE.md` §8).

---

## 5. Inbound API

Host: **Gateway.Api** (App Service in production; locally `http://localhost:5175` or `https://localhost:7176`).

Swagger in Development: `/swagger`.

There is **no URL prefix**. Controllers map:

| Method | Path | Timeout they enforce (spec) | Auth |
|---|---|---|---|
| `GET` | `/health` | connection-card / probe | Bearer location key |
| `GET` | `/menu` | **30s** | Bearer location key |
| `POST` | `/orders` | **10s** | Bearer location key + `Idempotency-Key` |

The store is resolved **only** from the Bearer key, not from `location_id` in the JSON body. The body field is accepted for their payload shape but is not trusted.

### 5.1 Authentication

```
Authorization: Bearer {LOCATION_KEY}
```

Unknown or missing key → **401**. During key rotation the previous key is still accepted for **24 hours** (`PreviousLocationKey` + `LocationKeyRotatedAtUtc`).

### 5.2 `GET /health`

Used for their connection card, alerting, and before auto-pausing a location.

- **200** `{ "status": "ok" }` — POS ping succeeded.
- **503** `{ "status": "degraded", "detail": "…" }` — no POS connection, or Pilot/GAAP ping failed.

A 503 here does **not** mean our SQL is down; it means the **till path** is unhealthy. Locally this stays 503 until a real Pilot key is saved on the store.

### 5.3 `GET /menu`

Pulls the bound POS catalogue and returns Order Harmony’s tree. Money is **integer minor units** (cents). `tax_rate_bp` is optional (basis points).

```json
{
  "categories": [
    {
      "external_id": "MAINS",
      "name": "MAINS",
      "products": [
        {
          "external_id": "3501",
          "name": "Item 3501",
          "description": null,
          "price_cents": 8500,
          "tax_rate_bp": null,
          "modifier_groups": [
            {
              "external_id": "Extras",
              "name": "Extras",
              "min_select": 0,
              "max_select": 1,
              "modifiers": [
                {
                  "external_id": "1582",
                  "name": "Option 1582",
                  "price_delta_cents": 0
                }
              ]
            }
          ]
        }
      ]
    }
  ]
}
```

**Rules for Order Harmony:**

- Persist `external_id` on products and modifiers. Those values must come back on `POST /orders`.
- Pilot does not publish min/max selection rules. We currently emit `min_select: 0` and `max_select` = option count. Do not treat those as till-enforced until Pilot confirms.
- No `menu.changed` webhook yet. Re-pull on a schedule (we also have an hourly worker re-pull for our own use; it does not yet notify OH).

**404** if the store has no POS connection.

### 5.4 `POST /orders`

Headers:

```
Authorization: Bearer {LOCATION_KEY}
Idempotency-Key: {uuid}
Content-Type: application/json
```

`Idempotency-Key` is **required**. A successful **201** is stored for **≥ 24h** and replayed verbatim on the same key (including the original `pos_order_id`). Failed injections are **not** cached, so a corrected retry with the same key is allowed.

#### Request body

Snake_case, Order Harmony spec. All money fields are **cents**. `placed_at` / `scheduled_for` are UTC ISO-8601 (`Z`).

| Field | Required | Notes |
|---|---|---|
| `order_ref` | yes | Their unique id. We persist it; webhooks echo it. |
| `display_id` | yes | Short ticket id (KDS / kitchen). |
| `source_channel` | yes | e.g. `uber_eats`, `direct_dine`, `test`. |
| `brand_name` | no | Ghost kitchen / sub-brand; passed to Pilot as `subBrand`. |
| `location_id` | yes | Informational; store comes from the Bearer key. |
| `fulfillment_type` | yes | `delivery` \| `pickup` \| `dine_in`. |
| `placed_at` | yes | UTC. |
| `scheduled_for` | no | UTC; held conceptually — till fire-at-time depends on POS. |
| `customer` | no | `name`, `phone`, `email`. |
| `delivery_address` | for delivery | `line1` required if present. |
| `items[]` | yes | See below. |
| `subtotal_cents`, `tax_cents`, `delivery_fee_cents`, `tip_cents`, `total_cents` | yes | Integers. |
| `currency` | yes | e.g. `ZAR`. |
| `payment.prepaid` | yes | Order Harmony orders are prepaid; we always tell the till the sale is already paid. |
| `notes` | no | Order-level. |

Each item:

| Field | Required |
|---|---|
| `external_product_id` | yes — POS PLU from `/menu` |
| `name` | yes |
| `quantity` | yes |
| `unit_price_cents` | yes |
| `total_price_cents` | yes |
| `notes` | no |
| `modifiers[]` | yes (empty array allowed) |

Each modifier:

| Field | Required |
|---|---|
| `external_modifier_id` | yes — POS option PLU from `/menu` |
| `name` | yes |
| `quantity` | yes |
| `price_delta_cents` | yes |
| `group_external_id` | no |

#### Example (pickup, one item + one option)

Proven against Pilot QA (`vendorId` 715 / `siteId` 13305):

```http
POST /orders
Authorization: Bearer dev-local-location-key
Idempotency-Key: 8f3c1a2b-6d90-4e21-9b7a-oh-test-001
Content-Type: application/json
```

```json
{
  "order_ref": "OH-TEST-3501-001",
  "display_id": "T1",
  "source_channel": "test",
  "location_id": "local-dev",
  "fulfillment_type": "pickup",
  "placed_at": "2026-08-31T07:30:00Z",
  "customer": {
    "name": "Test customer",
    "phone": "0820000000"
  },
  "items": [
    {
      "external_product_id": "3501",
      "name": "Item 3501",
      "quantity": 1,
      "unit_price_cents": 8500,
      "total_price_cents": 8500,
      "modifiers": [
        {
          "external_modifier_id": "1582",
          "group_external_id": "Options",
          "name": "Option 1582",
          "quantity": 1,
          "price_delta_cents": 0
        }
      ]
    }
  ],
  "subtotal_cents": 8500,
  "tax_cents": 0,
  "delivery_fee_cents": 0,
  "tip_cents": 0,
  "total_cents": 8500,
  "currency": "ZAR",
  "payment": {
    "prepaid": true
  }
}
```

#### Success

**201 Created**

```json
{
  "pos_order_id": "879205990"
}
```

`pos_order_id` is the id we use on our side (for Pilot, a derived numeric `orderId`). Pilot may also return their own till reference in logs (`Reference`); we do not currently put that on the 201 body.

For **delivery**, include `delivery_address.line1` (and city / postal as available). We map `fulfillment_type` to Pilot as `Collect` / `Delivery` / `Inhouse`.

---

## 6. Errors

Every non-success JSON error from the channel uses:

```json
{
  "code": "store_not_active",
  "message": "Store is Paused, not accepting orders.",
  "retryable": false
}
```

**`retryable` is operational.** Order Harmony retries up to 5 times over ~5 minutes on retryable failures, then flags for operator attention and may auto-pause the location. We must not mark a bad PLU or a paused store as retryable.

| HTTP | `code` | Retryable | When |
|---|---|---|---|
| 400 | `invalid_payload` | false | Missing `Idempotency-Key`, malformed body, unknown `fulfillment_type` |
| 400 | `pos_config_incomplete` | false | Store POS connection missing numeric vendor/site (or GAAP extras) |
| 400 | `pos_failure` | false | POS rejected the order (validation / bad PLU on till, etc.) |
| 401 | — | — | Missing or unknown Bearer location key |
| 404 | `unknown_location` | false | No store / no POS connection for menu |
| 404 | `unknown_plu` | false | POS could not resolve a product (when the adapter classifies it) |
| 409 | `store_not_active` | false | Store is Draft, Paused, or Deactivated |
| 409 | `store_closed` | false | Reserved — hours/closed path not fully wired yet |
| 422 | `modifier_rule_violation` | false | Reserved — we do not yet enforce min/max ourselves |
| 503 | `pos_failure` / degraded health | **true** if POS looks transient (timeout, 503, 504) | Till offline / upstream blip |

On **503** with `retryable: true`, Order Harmony should retry with the **same** `Idempotency-Key`. If the first attempt later succeeded, the replay returns the original **201**.

---

## 7. Outbound status webhooks

After a successful inject we record **Accepted** and enqueue `order.status_changed`. Further states come from the POS (Pilot callback → our `StatusSyncUseCase`) or, for GAAP later, a synthesizer.

### Payload (doc 02 §3)

`POST {ChannelConnection.WebhookUrl}`

```json
{
  "event_id": "stable-uuid-per-event",
  "event_type": "order.status_changed",
  "event_time": "2026-08-31T07:54:35.203Z",
  "order_ref": "OH-TEST-3501-001",
  "pos_order_id": null,
  "status": "accepted",
  "reason": null
}
```

`status` is only: `accepted` | `preparing` | `ready` | `completed` | `cancelled`. `reason` is set on cancel only.

`event_id` is stable across our retries. Order Harmony should dedupe on it. `event_time` is present because delivery order is not guaranteed.

### Signature (doc 02 §4)

```
X-OH-Timestamp: {unix_seconds}
X-OH-Signature: {lowercase hex HMAC-SHA256}
```

Message signed: `"{timestamp}.{raw_body}"` (UTF-8), HMAC-SHA256 with the location’s signing secret, hex **lowercase**. Verify against the **raw body bytes**, not a re-serialized JSON object.

### Retry / delivery

Backoff intended: **1s, 5s, 30s, 2m, 10m** on non-2xx (`ARCHITECTURE.md` §6). Implementation: Worker Service Bus trigger (`webhook-delivery` subscription, **sessions on**, `SessionId = store_id`) so two updates for the same store never run concurrently or out of order.

**Locally this path is idle** until Service Bus is configured and `OutboundWebhookDeliveryFunction` is enabled (`docs/LOCAL-DEV.md`). Injection can succeed without webhooks arriving.

We **never regress a terminal status**. A late `preparing` after `completed` is ignored.

---

## 8. Status lifecycle

The only status vocabulary in the gateway:

```
accepted → preparing → ready → completed
                ↘         ↘        ↘
                 cancelled (from any non-terminal state)
```

`completed` and `cancelled` are terminal. Same-status replays are no-ops.

Pilot: till callbacks map into this enum (only `statusCode` **2 = Pending → accepted** is confirmed; other codes are rejected rather than guessed). GAAP: no live kitchen feed — synthesizer later.

---

## 9. Timezones and money

- Order Harmony sends UTC (`Z`). We store UTC.
- Adapters convert to the store’s IANA timezone only when building the POS payload (Pilot wants naive `yyyy-MM-dd HH:mm:ss.fff`).
- All channel money is **integer cents**. Do not send rands as decimals on `/orders`.

---

## 10. How to test this channel today

Prerequisites: Docker SQL, API running, store **Active**, Pilot key tested and saved on the store (`docs/LOCAL-DEV.md`).

1. `GET /health` with the location key — expect **200** once Pilot answers.
2. `GET /menu` — copy real `external_id` values (PLUs).
3. `POST /orders` with those PLUs, a unique `order_ref`, and a unique `Idempotency-Key`.
4. Expect **201** and a ticket on Eddie’s Pilot till.
5. Repeat `POST` with the **same** `Idempotency-Key` — same **201** body, no second till ticket.
6. Webhooks: not local until Service Bus + Worker + Nick’s webhook URL.

`src/Gateway.Api/Gateway.Api.http` has starter calls. API console logs inbound `POST /orders` and the JSON we send to Pilot.

---

## 11. What Phase 2 still owes Nick

Inbound inject/menu/health against a live Pilot till is done. To close Phase 2 exit criteria (*“Nick can trigger a test order from DineDirect’s sandbox, see it land, and see status webhooks arrive”*):

1. **Environment** — public (or VPN) API base URL Order Harmony can call; static egress IPs if they allow-list us.
2. **Webhook URL + signing secret** exchanged for at least one sandbox location.
3. **Service Bus** topic `order-events`, subscriptions `webhook-delivery` (sessions) and `portal-live-feed`; Worker function enabled.
4. **Pilot status codes** beyond Pending, and callback payload confirmation — otherwise we cannot honestly emit `preparing` / `ready` / `completed`.
5. Turn the 14 skipped tests in `tests/Gateway.Api.CertificationTests` into real runs (Phase 3).

---

## 12. Certification checklist (Phase 3)

One automated test per row is planned. None run in CI until sandbox credentials exist.

| # | Scenario | Channel behaviour to expect |
|---|---|---|
| 1 | Happy-path delivery | **201**, address mapped, till ticket |
| 2 | Pickup, no address | **201**, `Collect` |
| 3 | Scheduled order | Accept `scheduled_for`; till hold/fire is POS-dependent |
| 4 | Duplicate `Idempotency-Key` | Same **201**, no second ticket |
| 5 | Unknown PLU | **404** `unknown_plu` or **400** `pos_failure`, `retryable: false` |
| 6 | Modifier min/max violation | **422** when we can enforce it |
| 7 | Store closed | **409** `store_closed` / `store_not_active` |
| 8 | Till offline | **503** `retryable: true`, then success on retry |
| 9 | Full status round-trip | Webhooks `accepted → preparing → ready → completed` |
| 10 | Cancellation | Webhook `cancelled` + reason |
| 11 | Menu pull | Stable `external_id`s |
| 12 | 86 within 30s | Not built |
| 13 | Bad webhook signature | They reject; we re-sign and retry |
| 14 | Multi-brand ticket | `brand_name` on the ticket |

---

## 13. Code map

| Piece | Project |
|---|---|
| Inbound controllers, location-key auth, HMAC signer, webhook HTTP | `Gateway.Adapters.OrderHarmony` |
| Order inject / menu / health / status use cases | `Gateway.Application` |
| Status state machine | `Gateway.Domain` (`OrderStatusTransition`) |
| Host Order Harmony calls | `Gateway.Api` |
| Outbox → Service Bus → signed POST | `Gateway.Infrastructure` + `Gateway.Worker` (`OutboundWebhookDeliveryFunction`) |
| Certification placeholders | `tests/Gateway.Api.CertificationTests` |
