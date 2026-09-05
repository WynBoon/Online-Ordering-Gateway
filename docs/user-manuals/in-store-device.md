# In-store device

Who it's for: kitchen staff at a store that takes Order Harmony (Dine Direct) orders. The till still prints or displays the kitchen ticket. This tablet is the **status of record** for preparing / ready / completed — Order Harmony never hears from GAAP, and a Pilot callback is optional backup.

Why it exists: GAAP Unity records a closed, paid sale and cannot send status back. Industry default is a tablet per store, the same shape as an Otter / Direct device. Functions are stipulated per store in the portal and **snapshotted onto each pairing code**. Changing the store default later does not widen an already-issued or already-paired device. Edit that row's checkboxes instead.

## Pairing

1. In the portal, open the store → **In-store devices**.
2. Tick the functions this kitchen is allowed to perform. **Save store default** if you want new codes to copy them.
3. Name the device (default "Pass kitchen") → **Issue pairing code**.
4. Enter the six-digit code on the tablet within 15 minutes. It is shown once; the portal stores only a hash.
5. The tablet receives a device token (also hashed at rest). Heartbeat, open orders, and actions use `Authorization: Bearer {token}` against the gateway API — not the portal.

Revoke stops heartbeat, order list, and actions immediately (token hash cleared, status Revoked). Issue a new code to replace a lost tablet.

## Buttons → Order Harmony status

| Tablet button | Required function | Order Harmony status |
|---|---|---|
| Start preparing | Mark preparing | `preparing` |
| Mark ready | Mark ready | `ready` (walks `accepted` → `preparing` → `ready` if needed, each step webhooked) |
| Handed over | Mark completed | `completed` |
| Running late +5 min | Adjust promise time | No extra status; bumps promise time 1–60 minutes |
| Cancel order | Cancel order | `cancelled` with a reason |

If a function is not granted, the button is hidden on the tablet **and** the API rejects the action (`400`, no outbox). Phone/email and notes are omitted unless those view flags are on.

GAAP's sales poll may still confirm Completed from `TENDERED` or Cancelled from `CANCELED`. It never fakes preparing or ready.
