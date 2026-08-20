# Pilot Live — kickoff call agenda

**Purpose:** confirm the technical details of `OnlineOrder/Create` and its supporting
endpoints so we can build our online-ordering integration against Pilot.
Framing for the call: we're integrating an online ordering platform into Pilot
for a group of restaurant sites — no need to mention other POS platforms.

We've already reviewed the published Swagger
(`openapi.pilotlive.co.za/swagger/v1/swagger.json`), so most of this is
confirming/filling gaps, not discovery from scratch.

---

## What we already know (don't need to ask)

- Order injection is `POST /OnlineOrder/Create`, taking `vendorId`, `siteId`,
  `orderId`, `items[]`, `payments`, `delivery`, `orderStatus`, and a
  `callbackUrl` we supply per order.
- Auth is `POST /Authorization/Token` — a global API key exchanged for a
  short-lived JWT scoped to `VendorId`/`StoreId`.
- Menu comes from `GET /SalesProducts/Menu`.
- Store status from `GET /Store/Status`, `GET /Store/Information/{StoreId}`.
- Health check is `GET /api/Health/Check`.

## 1. Order lifecycle & status feedback (highest priority)

- `orderStatus.statusCode` is an integer with a `description` string — we
  need the **full code table**. What are all the valid codes, and which ones
  are terminal (completed/cancelled) vs in-progress?
- We supply a `callbackUrl` per order. **What exactly gets POSTed to it** —
  the same `OnlineOrderRequest` shape with an updated `orderStatus`, or a
  different, smaller payload?
- Is the callback **signed or otherwise verifiable**, so we know it genuinely
  came from Pilot and not a spoofed request?
- If our callback endpoint is briefly down or returns a non-2xx, does Pilot
  **retry**, and on what schedule? Anything we need to do to avoid missing an
  update permanently?
- Is there a way to **cancel** an order after injection — a dedicated
  endpoint, or do we resend `OnlineOrder/Create` with `orderStatus` set to a
  cancelled code?

## 2. Idempotency & retries

- If we retry `POST /OnlineOrder/Create` with the same `orderId` /
  `orderReference` (e.g. because our request timed out but actually
  succeeded), does Pilot **detect the duplicate**, or would it create a
  second order on the till? What field should we treat as the dedupe key?
- What's the **expected timeout** for the create call, and is there a
  recommended retry/backoff pattern on our side?

## 3. Payments

- `payments.status` accepts `PAID`/`UNPAID`, and `payment.paymentMethod`
  lists `Cash`, `CreditCard`, `EFT` in the docs. For an order that's **already
  been paid on our platform** before it reaches the till, which
  `paymentMethod` value should we use — is there a dedicated value for
  "settled externally," or should we use one of the three listed?

## 4. Provisioning / onboarding a new site

- What's the actual process to get a `vendorId` and a `siteId` for a new
  restaurant location — `Authorization/Register` vs `RegisterList` vs
  `RegisterCsv` in the docs aren't self-explanatory. Which one should we
  use, and who initiates it (us or Pilot)?
- Token TTL and refresh — how long is the JWT valid (`exp` claim), and is
  there a documented refresh flow, or do we just call `Token` again before
  expiry?

## 5. Menu & catalogue

- Are `plu` values on products and `OrderItemOption` **stable across menu
  pulls** — can we treat them as permanent mapping keys?
- Modifier groups — the spec doesn't show min/max selection rules anywhere.
  Is there a concept of required/optional modifier groups, or is that
  entirely managed on the till side with no validation from our end?
- Any webhook or push notification when the **menu changes** on the till, or
  do we need to poll `SalesProducts/Menu` on a schedule?

## 6. Stock / availability

- Is there any real-time way to know an item has been **86'd** (marked out
  of stock) on the till, so we can reflect that on the ordering platform? We
  didn't find anything in the current API surface — confirming it doesn't
  exist yet, or whether it's on a roadmap.

## 7. Scheduling & order types

- Does Pilot support a **scheduled/future order** — i.e., hold an order and
  fire it at a specific time — or does every order go straight to the till
  on injection? (`orderedDate` vs `createdDate` in the schema suggests maybe,
  but not clearly.)
- `deliveryMethod` lists `Collect`, `Delivery`, `Inhouse` — is that the
  complete list?
- `subBrand` field exists for ghost-kitchen setups — does it print
  distinctly on the ticket, same as the field name implies?

## 8. Environments & rate limits

- Sandbox base URL and how we get a **test site** provisioned.
- Per-vendor / per-site **rate limits** we should design around.
- Does Pilot have its own certification/sign-off checklist before going live
  with a new integration, similar to a standard partner onboarding process?

---

## Suggested order to run through on the call

1. Lifecycle & status feedback (§1) — this is the thing our whole design
   depends on.
2. Idempotency & payments (§2–3) — correctness-critical, quick to answer.
3. Provisioning (§4) — needed before we can even get a sandbox site working.
4. Menu, stock, scheduling (§5–7) — good detail, less urgent than the above.
5. Environments & process (§8) — wrap-up, logistics.

If time runs short, §1 and §4 are the two sections we cannot proceed without.
