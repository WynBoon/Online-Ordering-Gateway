# MOOG Store Device: UI Specification v1.2

**Product:** OnlineOrder Gateway, in-store order device
**Scope:** UI/UX only. Layout, components, states, motion, sound, accessibility, acceptance criteria. No backend changes are specified; the few data fields the UI needs are listed in section 14.
**Date:** 19 Sep 2026 (v1.2: device is status-update only and never prints; v1.1: hardware, brand, arrival events, multi-device)

---

## 0. Assumptions (correct these first)

I have not seen the current screens, so this spec is written as a replacement design rather than a redline.

| # | Assumption | If wrong |
|---|---|---|
| A1 | Android tablet (confirmed), 10–11", landscape, standing next to the POS, running full-screen in kiosk mode. Model and OS version still unknown | Portrait and 8" layouts are covered in section 5; a phone form factor is not |
| A2 | Reference canvas: **1280 × 800 dp** (typical 11" tablet at ~1.5x density) | Layout is fluid; only breakpoints change |
| A3 | Staff are standing at the POS, often wet or gloved hands, screen viewed from 0.5–1.5 m (counter, not a kitchen pass), noisy room | Drives every size and sound number below. The 2 m readability target stays as a safety margin |
| A4 | The device is the **status of record** (the POS, e.g. Harmony, can print a ticket but cannot report accepted/ready/collected back) | If a POS can report status, the "POS sync" chip in section 6.3 becomes informational |
| A5 | POS-agnostic: the UI must never show POS-specific vocabulary (Pilot, GAAP Unity, Harmony) | n/a |
| A6 | Framework-neutral. Tokens are in dp/sp and hex so they port to .NET MAUI, native Android or web | Section 15 has MAUI notes |
| A7 | **The device never prints.** It is an order status surface only (accept, ready, handed over). The POS prints the kitchen ticket itself. No print, printer, or print-status UI exists anywhere in this product | If print is ever added, it is a separate spec |

---

## 1. Why the current UI feels average, and what "award-level" means here

Utility screens look average when every element has the same weight. The fix is a strict hierarchy: **one number, one timer, one button per ticket**, everything else quieter.

I used Apple's Design Award categories as the quality rubric, because they are a public, well-defined bar: Delight and Fun, Inclusivity, Innovation, Interaction, Social Impact, and Visuals and Graphics. Social Impact does not apply to this product. The other five map to concrete requirements:

| Rubric | What it means for this device | Where |
|---|---|---|
| Interaction | One-tap accept, no hidden gestures, undo instead of confirm dialogs, sub-100 ms response | 6, 8 |
| Inclusivity | WCAG 2.2 AA everywhere, AAA contrast for primary text, no colour-only meaning, text scaling to 140% | 10 |
| Visuals and Graphics | Single type family with tabular numerals, disciplined 6-colour semantic palette, tonal elevation | 3, 4 |
| Innovation | Kitchen-load-aware prep-time suggestion, pre-flight check, POS sync truth per order, handover mode | 7 |
| Delight | Calm when idle, one considered motion at the moment that matters, a satisfying "cleared" transition | 9 |

---

## 2. Research findings that drive the design

Sources are named so you can verify them. Findings are paraphrased.

**Order-alert behaviour**
- Uber Eats' merchant tablet flashes green and plays a sound on a new order, and a tap anywhere opens it. It exposes Busy Mode, Pause new orders, delay, cancel and out-of-stock as first-class controls (Uber Eats Merchant Academy).
- Toast Orders Hub uses five status tabs with counts (Needs Approval, Scheduled, Active, Order Ready, Complete), a blue dot on unread orders, an online-ordering control with on/off, snooze and added delay, and long-press multi-select for bulk actions (Toast platform guide and support).
- Lightspeed offers "repeat alert sound until all new orders have been accepted" as a setting (Lightspeed Restaurant support).
- Missed orders are the dominant failure mode. Staff miss pings while serving, and unaccepted delivery-app orders can auto-cancel (Foodhub, Squarespace forum thread, me&u guide). Providers add SMS or automated phone-call fallbacks for exactly this reason.

**Practitioner references (weaker evidence, useful specifics)**
- Two open-source restaurant-tablet projects on GitHub converged on: keep sounding until **Accept** is pressed, not until the order is merely opened (a glance or mis-tap silenced the kitchen); make Accept the largest control on the screen; compute ticket age from server time so a wrong tablet clock cannot make a late ticket look fresh; escalate to the office when an order is unaccepted for 10 minutes.
- A Square patent describes a green, orange, red ticket scheme at 0–5, 5–10 and 10+ minutes. One of the GitHub projects uses amber at 10:00 and red at 15:00. Thresholds vary by kitchen, so they must be configurable (section 6.4).

**KDS visual language**
- Kitchen display vendors converge on colour-coded tickets, time-based colour change, adjustable text size, modifiers that stand out, and strike-through for completed items (Toast, Square, Lightspeed, TouchBistro, Menusifu). A published KDS redesign case study flagged overuse of red as a readability failure, so red is reserved for one meaning here (late/failed).

**Accessibility floors**
- WCAG 2.2 SC 2.5.8 (AA) requires targets of at least 24 × 24 CSS px; SC 2.5.5 (AAA) asks for 44 × 44. Apple recommends 44 pt, Material 48 dp, visionOS 60 pt. WCAG 2.2 also added 2.5.7, which requires a non-dragging alternative for drag gestures. (WCAG documentation, LogRocket target-size roundup.)

**South African operating context**
- Nationwide load-shedding had not been implemented for well over a year as of mid-2026, but localised and unannounced outages persist and Eskom has said cuts could return. POS guides advise that offline behaviour, UPS for router/printer, and charged devices remain essential. The UI must therefore handle power and connectivity loss gracefully rather than assume they are solved.

---

## 3. Design principles

1. **One glance, one action.** From 2 m a person can tell how many orders are waiting, which is most urgent, and where to tap.
2. **Silence is a bug.** An unaccepted order never goes quiet. A failure never fails quietly.
3. **The screen tells the truth about sync.** Because this device is the status of record, every order shows whether the POS received it. Printing is the POS's job and is invisible to this device.
4. **Built for wet hands.** Large targets, generous spacing, no precision gestures, undo over confirm.
5. **Calm until it matters.** Idle screens are still. Motion and colour saturation are spent only on new, late and failed.

---

## 4. Visual system

### 4.1 Themes

- **Service (dark), default.** Lower glare in kitchens, and state colours read as light sources.
- **Daylight (light).** For bright counters or windows. Auto-switch is off by default; the manager chooses it in Settings.

### 4.2 Colour tokens

All ratios were computed against WCAG 2.x relative luminance.

**Service (dark)**

| Token | Hex | Use | Contrast |
|---|---|---|---|
| `bg` | `#0B0F14` | App background | n/a |
| `surface-1` | `#121820` | Columns, header | n/a |
| `surface-2` | `#1A222C` | Ticket cards | n/a |
| `surface-3` | `#232D39` | Sheets, pressed state | n/a |
| `outline` | `#313D4B` | Decorative dividers only | 1.6:1 (decorative) |
| `outline-strong` | `#66778A` | Control borders, focus-adjacent | 3.5:1 on surface-2 (meets 3:1 non-text) |
| `text-1` | `#F3F6FA` | Primary text | 14.8:1 on surface-2 |
| `text-2` | `#B4BFCC` | Secondary | 8.6:1 |
| `text-3` | `#8593A3` | Tertiary, captions | 5.1:1 |
| `new` | `#5AA9FF` | New / needs acceptance | 6.5:1 |
| `prep` | `#B39DFF` | Preparing | 7.0:1 |
| `ready` | `#4ADE80` | Ready | 9.2:1 |
| `warn` | `#FFB454` | At risk, customer notes | 9.1:1 |
| `late` | `#FF6B6B` | Late, failed, destructive | 5.8:1 |
| `on-color` | `#06121F` | Text on solid state fills | 6.8:1 to 10.8:1 |

**Daylight (light)**

| Token | Hex | Contrast on white |
|---|---|---|
| `bg` / `surface` | `#F4F6F8` / `#FFFFFF` | n/a |
| `text-1` / `text-2` / `text-3` | `#0F1720` / `#3F4B5A` / `#5C6877` | 18.1 / 8.9 / 5.7 |
| `new` / `prep` / `ready` | `#0B5FD6` / `#6B3FD4` / `#0F7A3B` | 5.8 / 6.4 / 5.4 |
| `warn` / `late` | `#8F5200` / `#C5221F` | 6.2 / 5.8 |
| `outline-strong` | `#7B8794` | 3.7 |
| Solid state fills use `#FFFFFF` text | n/a | 5.4 to 6.4 |

**Colour rules**
- Colour is never the only signal. Every state also has an icon, a text label and a position (column). This also keeps the red/green pair safe for colour-blind staff.
- Red means exactly two things: **late** and **failed**. It is never used for decoration or "cancel" buttons that are not destructive.
- Card colour equals button colour equals state colour. A blue New card has a blue Accept button.

### 4.3 Typography

One family: **Inter** (variable), bundled with the app. Enable **tabular figures** and **slashed zero** so timers never jitter and 0/O are unambiguous.

| Role | Size / line (sp) | Weight | Notes |
|---|---|---|---|
| Order number | 40 / 44 | 700 | The largest text on a card |
| Timer | 32 / 36 | 600 | Tabular figures, `mm:ss` |
| Section title (column) | 20 / 24 | 600 | Letter-spacing +2%, caps |
| Item quantity | 24 / 28 | 700 | In a 44 dp gutter |
| Item name | 22 / 28 | 500 | Max 2 lines on the board, full on detail |
| Modifier | 18 / 24 | 500 | `prep` or `text-2`, prefix `+` / `−` |
| Customer note | 18 / 24 | 500 italic | `warn`, in a bordered callout |
| Body | 16 / 22 | 400 | Detail and settings |
| Caption | 14 / 18 | 500 | Absolute minimum; never for anything actionable |

**Text scale setting:** Standard 100%, Large 120%, XL 140%. Layout must not break at 140% (cards grow; columns scroll).

### 4.4 Shape, spacing, elevation

- Spacing scale (dp): 4, 8, 12, 16, 24, 32. Card padding 16. Gap between cards 12. Column padding 16.
- Radius: cards 16, sheets 24, buttons 14, chips 999.
- Elevation is tonal (surface-1 to surface-3), not shadow. One exception: the New-order takeover uses a 60% scrim.
- Icons: 2 dp stroke, 24 dp grid, rounded caps, one family (Lucide or Phosphor), always paired with text on primary actions.

---

## 5. Layout

### 5.1 Breakpoints

| Width | Layout |
|---|---|
| < 900 dp or portrait | Single column with bottom bar: Live · Scheduled · History · Menu. Live shows tabs New / Preparing / Ready |
| 900–1400 dp (reference) | Three columns: New · Preparing · Ready. Detail opens as a right-side sheet, 480 dp |
| > 1400 dp | Three columns plus a persistent 440 dp detail pane |

### 5.2 Live Board (default screen), 1280 × 800 dp

```
┌─────────────────────────────────────────────────────────────────────────────────────────┐
│ Store name      [ Live 7 | Scheduled 2 | History | Menu ]      ORDERING [● Open ▾]  ◉ ▮▮▮  14:32 │ 72
├─────────────────────────────────────────────────────────────────────────────────────────┤
│ Avg accept 0:18   Avg prep 12:40 / 15:00   On time 94%   Waiting to be collected 1          │ 40
├───────────────────────┬───────────────────────┬────────────────────────────────────────┤
│ NEW  2                │ PREPARING  4          │ READY  1                                 │ 48
│ ┌───────────────────┐ │ ┌───────────────────┐ │ ┌────────────────────────────────────┐ │
│ │ 4271     ⏱ 0:12   │ │ │ 4268     ⏱ 06:10  │ │ │ 4265           ⏱ waiting 03:40      │ │
│ │ Sipho M · Paid    │ │ │ Lerato K · Cash   │ │ │ Thabo N · Paid                      │ │
│ │ 2  Chicken Burger │ │ │ ...               │ │ │ ...                                 │ │
│ │    + No onion     │ │ │                   │ │ │                                     │ │
│ │ 1  Large Chips    │ │ │ [   Mark ready  ] │ │ │ [        Handed over        ]       │ │
│ │ ┌ Note: allergy ┐ │ │ └───────────────────┘ │ └────────────────────────────────────┘ │
│ │ [Ready 14:47 ▾]   │ │                       │                                         │
│ │ [    Accept     ] │ │                       │                                         │
│ └───────────────────┘ │                       │                                         │
└───────────────────────┴───────────────────────┴────────────────────────────────────────┘
```

**Column behaviour**
- Column headers are sticky, show live counts, and carry the state icon.
- **New** is sorted by arrival (oldest first). **Preparing** is sorted by promised-ready time, soonest first, so the most urgent ticket is always top-left of the working area. **Ready** is sorted by time waiting, longest first.
- Cards never reorder while a finger is down on the board. Reordering is queued until touch-up plus 400 ms.
- Empty column shows a single quiet line and icon, never a large illustration (calm principle).

### 5.3 Header bar (72 dp)

- **Left:** store name (16 sp, `text-2`) with a 10 dp status dot.
- **Centre:** segmented control, four items, each ≥ 120 × 56 dp. Counts in a pill.
- **Right cluster**
  - **Ordering control:** `Open`, `Busy (+10 min)`, `Paused`. Tapping opens a bottom sheet with three 96 dp-tall options, a duration row (15 / 30 / 60 min / until I reopen), and a plain-language consequence line ("Customers will see 25 min instead of 15"). This follows the Uber Eats Busy/Pause and Toast Snooze/Delay patterns. Paused state turns the whole header amber so it cannot be forgotten.
  - **Connection:** icon plus text on tap (section 8.4).
  - **Battery:** icon plus % below 30%, `warn` below 20%, `late` below 10% and charging-not-detected.
  - **Clock:** 24-hour, tabular, seconds hidden.

### 5.4 Pace strip (40 dp)

Four numbers, read-only, refreshed every 30 s. Its purpose is to make the manager's decision ("go Busy?") obvious. Numbers are computed over the last 10 completed orders or 60 minutes, whichever is fewer.

---

## 6. Components

### 6.1 Ticket card

**Anatomy, top to bottom**

1. **Header row (64 dp):** order number (40 sp) left; fulfilment chip (Collection / Delivery / Dine-in, icon plus text); timer chip right.
2. **Identity row (32 dp):** first name and surname initial; payment chip. **Paid** is neutral; **Pay on collection** is `warn` filled so it cannot be missed at handover.
3. **Item list:** quantity gutter 44 dp, name, modifiers indented 16 dp. Removals use `−` and strike-through, additions `+`.
4. **Customer note callout:** `warn` 2 dp border, 8 dp radius, note icon. Always fully visible, never truncated. Allergy or dietary keywords (a configurable list) are **highlighted in bold as a visual aid only**; the product must never claim to detect allergies.
5. **Sync row (24 dp):** POS status chip (6.3).
6. **Primary action (64 dp high, full width):** label plus, on New, the promised ready time.
7. **Overflow (48 × 48 dp):** Details, Add time, Decline / Cancel.

**Density:** the board shows up to 6 item lines per card, then `+N more items` (48 dp tall, tappable, expands in place). **New cards never collapse.**

**State treatments**

| State | Left stripe (6 dp) | Header | Primary button | Timer meaning |
|---|---|---|---|---|
| New | `new` | Blue dot until acknowledged | **Accept** (`new` fill) | Time waiting, counts up |
| Preparing | `prep` | Icon: pan | **Mark ready** (`prep` fill) | Time remaining to promise, counts down; `+mm:ss` when overdue |
| Ready | `ready` | Icon: bag | **Handed over** (`ready` fill) | Time waiting for collection, counts up |
| Late (overlay) | `late` | Pulsing 2 dp `late` border | unchanged | Timer chip turns `late` fill |
| Failed to reach POS | `late` | Banner across card top | **Retry** | n/a |

### 6.2 Accept interaction (the most important 400 ms in the product)

- Prep-time chips sit above Accept: **10 · 15 · 20 · 30 · Custom**. One is pre-selected using the load-aware suggestion (section 7.1). The Accept label reads e.g. **Accept · ready 14:47**.
- **One tap = accept with the suggested time.** Two taps = change time, then accept.
- On tap: card animates to the Preparing column (320 ms, section 9); alert sound stops immediately; a snackbar shows **Accepted · Undo** for 8 s.
- Undo is honoured only until the POS has acknowledged the order (typically under 5 s); after that Undo turns into "Cancel order" behind a confirm sheet.

### 6.3 POS sync chip (per order)

Because the device is the status of record, each card shows the POS delivery truth:

| Chip | Meaning | Colour |
|---|---|---|
| `Sending to POS…` | In flight, under 5 s | `text-2` |
| `In POS` | POS acknowledged | `text-2`, check icon |
| `Not in POS · Retry` | Failed or timed out after 10 s | `late`, whole card gets failed banner |

`Retry` resends the order to the POS with the **same idempotency key**, so a retry can never create a duplicate order. The chip never mentions printing: whether the POS prints a ticket is outside this device's knowledge.

### 6.4 Timers and thresholds (all configurable, defaults below)

| Situation | Normal | At risk (`warn`) | Late (`late`) |
|---|---|---|---|
| New, unaccepted | 0–60 s | 60–180 s | over 180 s |
| Preparing | under 75% of promised prep time elapsed | 75–100% | over 100% |
| Ready, waiting for collection | 0–10 min | 10–20 min | over 20 min |

- All timers derive from **server time**. The app measures its clock offset from server response headers and applies it, so a wrong tablet clock cannot skew timers.
- Timers tick at 1 Hz using tabular figures. Under 60 s show `s`, otherwise `m:ss`, over 60 min show `h:mm`.

### 6.5 Buttons

| Type | Height | Min width | Use |
|---|---|---|---|
| Primary | 64 dp | 200 dp | One per card or sheet, solid state colour |
| Secondary | 56 dp | 120 dp | Outlined `outline-strong`, text `text-1` |
| Destructive | 56 dp | 120 dp | `late` outline; solid only inside the confirm sheet |
| Icon | 56 × 56 dp | n/a | Visual glyph 24 dp |

- Minimum gap between adjacent targets: 12 dp. Primary target size is more than double WCAG's AAA 44 px floor, on purpose.
- Pressed: `surface-3` overlay plus 2% scale-down over 80 ms. Disabled: 40% opacity plus an explanatory line, never a silent dead button.

### 6.6 Sheets and dialogs

- Bottom sheets (24 dp top radius) for Ordering control, Decline reasons, Add time, Menu item availability.
- Dialogs only for irreversible actions (cancel an accepted order). Two buttons, destructive on the left, safe on the right, both 56 dp.
- **Undo replaces confirm** for reversible actions (Accept, Mark ready, Handed over). This is faster and lowers error cost.

---

## 7. Signature features (the "Innovation" tier)

### 7.1 Load-aware prep-time suggestion
Suggested time = median actual prep time of the last 10 orders for the same fulfilment type, plus 2 min per order currently Preparing beyond 3, rounded to the nearest chip. The suggestion is shown as a chip highlight only; the manager can override it and the override is remembered for that session. The header **Busy** button pushes the offset to customers via the existing gateway delay mechanism.

### 7.2 Pre-flight check (shift start)
Runs on first launch each day and from Settings. Four checks with live results, each ≤ 3 s:
1. Gateway connected
2. POS reachable (test ping)
3. **Alert sound test** (plays the tone at the enforced volume; user confirms "I heard it")
4. Battery ≥ 40% or charging

Result is one green **Ready for service** banner or a red list of what to fix. Pass state is shown as a small check in the header until midnight.

### 7.3 Handover mode
A full-screen view for the collection counter, toggled from the Ready column header. It shows large order numbers with first names (one per 96 dp row, up to 8 visible) and payment status, sorted by wait time, so staff and customers can match a bag to a person in one glance. Toast offers a similar guest-facing "order ready" board as a separate product feature. Tapping a row opens Handed over with 8 s undo. Touch targets are unchanged; no personal data beyond first name and initial is ever shown.

### 7.4 Multi-select bulk actions
Long-press a card (600 ms) or tap **Select** in the column header. Then **Mark ready** or **Handed over** applies to all selected. A visible Select button exists because long-press is not discoverable, and no swipe or drag is required (WCAG 2.5.7).

### 7.5 Customer-changed and customer-cancelled orders
If an order is modified upstream after acceptance, the card gets a **Changed** banner that stays until acknowledged; changed lines are outlined and marked with a `Δ` icon. A customer cancellation plays the error tone once, keeps the card in place with a `late` banner **Cancelled by customer** and requires a single tap to clear so nobody keeps cooking it.

---

## 8. Critical flows and states

### 8.1 New-order alert ladder

| Time since arrival | Sound | Visual | Notes |
|---|---|---|---|
| 0–60 s | Two-note tone, 0.9 s, repeats every 8 s | Blue pulsing card in New; screen wakes; header banner **1 NEW ORDER** (56 dp) on every screen including Settings and Menu | Sound stops only when **Accept or Decline** is pressed. Opening or viewing an order does not silence it |
| 60–180 s | Repeats every 4 s, +10% level | Banner grows to 96 dp with amber tint; screen brightness forced to max | n/a |
| over 180 s | Every 3 s | **Modal takeover** with the order summary and one Accept button | This is the only blocking modal in the product |
| configurable, default 5 min | n/a | Banner turns `late` | Gateway sends an SMS or push to the manager (backend action, UI shows "Manager alerted") |

- Multiple new orders stack in one banner as **3 NEW ORDERS**; the tone does not multiply.
- Do not use per-channel or per-customer sounds; one sound means one thing.

### 8.2 Decline / cancel flow
Overflow > **Decline** opens a sheet with four 72 dp reason rows: *Too busy*, *Item unavailable*, *Closing soon*, *Other*. If *Item unavailable*, the next step lists the order's items with a toggle to mark each as unavailable in the menu, so one flow both rejects the order and updates availability (Uber Eats supports marking items out of stock from the order screen).

### 8.3 Menu availability screen
Category tabs on the left rail (max 8), item list right, search field top. Each row is 72 dp with a 64 × 40 dp switch. Turning an item off asks **Until: 1 h · End of day · Until I turn it on**. Off items are shown at 50% with the reason and time. Search results ≥ 3 characters, debounced 150 ms.

### 8.4 Connection states

| State | Header | Board | Behaviour |
|---|---|---|---|
| Live | Dot `ready` | Normal | n/a |
| Reconnecting | Dot `warn`, "Reconnecting…" | Normal, sync chips show `Waiting` | Retry with backoff 1, 2, 4, 8 s, max 15 s |
| Offline | Full-width `late` banner: **Offline. New orders cannot arrive. Last synced 14:02** | Existing tickets stay visible and actionable | Actions queue locally with a `Pending sync` chip; the UI never shows a synced state it doesn't have |
| Stale (no heartbeat for over 15 s but socket open) | Same as Reconnecting | n/a | Prevents silent half-open connections |

On power loss the device is expected to be on battery: below 20% show a persistent amber strip **Battery 18%. Plug in to keep receiving orders**.

### 8.5 Other edge cases

| Case | Behaviour |
|---|---|
| Scheduled order | Lives in **Scheduled** with target time; appears in New at a configurable lead time (default 20 min before due), with a `Scheduled 18:30` chip |
| Two or more devices in one store | One shared order state on the server. Accepting on any device silences the alert on **all** devices within 1 s. A card being acted on elsewhere shows `Handled on Device 2` and disables its button for 2 s to prevent double taps. Each device has a name (Front, Counter) shown in Settings and the header. Only one device is the alert owner at 60 s; after 60 s all devices escalate together |
| 20+ line order | Card shows 6 lines then expander; detail sheet scrolls; header stays pinned |
| Very long item or note text | 2-line clamp on board, full on detail; notes are never clamped |
| Duplicate delivery of same order | Deduplicated by order reference; second event is silent (no second alert) |
| Screen dims or device sleeps | Prevented in app (wake lock); brightness floor 60% |
| App restart mid-service | Restores state from server in ≤ 2 s and never replays sounds for already-accepted orders |
| First run | Pairing screen with a 6-digit code and QR; no login form on a shared tablet |

### 8.6 Settings (behind a 4-digit manager PIN)
Sound (volume floor, test tone), theme, text size, thresholds (section 6.4), station name, diagnostics (app version, gateway latency, last 20 events), **Pre-flight**. Settings is never reachable by accident: 1 s press on the store name plus PIN.

---

## 9. Motion

- Durations: **120 ms** (state feedback), **200 ms** (fades, chips), **320 ms** (card moves between columns). Easing: `cubic-bezier(0.2, 0, 0, 1)`.
- Card moving to the next column: shared-element slide, then a 200 ms settle. Counts in column headers tick with a 120 ms fade.
- **Only two continuous animations exist in the product:** the New-order pulse (1.2 s loop, 2 dp border glow) and the Late border pulse (1.6 s loop). Nothing else moves.
- Reduce Motion (OS setting or in-app): pulses become a static 4 dp border; slides become crossfades.
- Performance budget: 60 fps with 30 cards on a 2–3 GB tablet. Virtualise lists; no blur effects; no full-screen shaders.

---

## 10. Sound

| Cue | Description | Trigger |
|---|---|---|
| New order | Two-note rising, 0.9 s, energy concentrated 1–4 kHz for audibility over kitchen noise | New arrival, repeats per 8.1 |
| Escalation | Same tone, faster and louder | After 60 s |
| Error | Single falling tone, 0.6 s | Cancelled by customer, POS failure |
| Confirmation | Soft click, 60 ms | Optional, off by default |

- Play through the **Android alarm audio stream**, so Do Not Disturb and media volume cannot silence it, and lock the app-level volume floor to 80% in kiosk configuration.
- Optional spoken cue "New online order" (TTS) for very loud kitchens, off by default.
- Sounds are never used as the only alert: the visual ladder in 8.1 always runs in parallel.

---

## 11. Accessibility (target: WCAG 2.2 AA, with AAA on primary text and touch targets)

- Contrast: primary text ≥ 7:1 (achieved 14.8:1), all text ≥ 4.5:1, non-text UI ≥ 3:1.
- Targets: minimum 48 dp for everything, 56 to 64 dp for primary actions (exceeds 2.5.8 and 2.5.5).
- No drag-only or swipe-only actions (2.5.7). Every gesture has a button.
- Colour independence (1.4.1): every state has icon, label and position.
- Text scaling to 140% without loss of function (1.4.4).
- TalkBack: every card is one focusable group with a spoken summary, e.g. "Order 4271, new, two items, waiting 12 seconds, Accept button".
- Focus order: header, columns left to right, cards top to bottom, primary action last within a card.
- Reduce Motion, high-contrast theme (uses AAA palette, borders at 4.5:1), and a left-handed toggle that mirrors the overflow and detail sheet side.
- Localisation: all strings externalised; layouts tolerate +30% string length. English at launch; Afrikaans and isiZulu strings supported in the resource structure.

---

## 12. Instrumentation and success metrics

Log these UI events (device-side timestamps corrected by server offset): `alert_started`, `alert_acknowledged`, `accept`, `undo`, `ready`, `handed_over`, `decline_reason`, `offline_enter`, `offline_exit`, `preflight_result`.

| Metric | Target |
|---|---|
| Median time to accept | ≤ 25 s |
| 95th percentile time to accept | ≤ 90 s |
| Orders unaccepted over 5 min | < 0.5% |
| Undo rate (mis-tap proxy) | < 3% |
| Taps to accept | 1 (default time) |
| Cold start to usable board | < 2 s |
| Tap-to-visual-feedback | < 100 ms |
| Unit test: readable from 2 m at Standard size | Pass (on-device check with the actual tablet) |
| Usability test, 5 staff, 3 tasks (accept, decline with reason, mark item unavailable) | 100% completion without help; SUS ≥ 85 |
| Accessibility audit | 0 critical, 0 serious findings |

---

## 13. Deliverables checklist for the designer and developer

1. Figma library: tokens (both themes), Inter variable setup, all components in every state, auto-layout, at 1280 × 800, 960 × 600 and 800 × 1280.
2. Clickable prototype covering flows 8.1, 8.2, 8.3, 8.4 and 7.2.
3. Sound files (WAV/OGG, −14 LUFS), delivered with a loudness report.
4. Motion spec (Lottie or coded) for the card transition and both pulses.
5. Component-level acceptance tests mapped to sections 6 to 11.
6. A real-tablet test in the noisiest kitchen: sound audible at 3 m with the extractor fans on, board readable at 2 m under glare.

---

## 14. Data the UI needs from the gateway

These are the only backend touchpoints implied by this spec:

| Field | Why |
|---|---|
| `order_ref`, `display_number` (3–4 digits) | Big card number and handover mode |
| `created_at`, `promised_ready_at` (server time) | All timers, thresholds and the Accept label |
| `fulfilment_type` | Collection, Delivery, Dine-in chip |
| `customer_first_name`, `surname_initial` | Identity row; never show full phone on the board |
| `payment_status` (Paid / Pay on collection) | Handover safety chip |
| `items[]` with `qty`, `name`, `modifiers[]` (with add/remove), `note` | Card body |
| `pos_sync_status` (sending, in_pos, failed) | Sync chip |
| `updated_upstream` flag and changed-line markers | Changed and cancelled banners |
| Server `Date` header or `server_now` | Clock offset |
| Ordering-state endpoint (open, busy +N, paused, until) | Header control |
| Item availability endpoint | Menu screen and decline flow |

---

## 15. Implementation notes

- **Kiosk and audio:** Android lock-task mode, screen wake lock, immersive full-screen, alarm audio stream, boot-on-power auto-launch.
- **.NET MAUI:** use `CollectionView` with virtualisation for columns; bundle Inter and enable tabular figures via the font's OpenType features (if the MAUI text renderer does not expose them, use a fixed-width digit style for timers, or render timers with SkiaSharp text). Implement pulses with `Animation` on border stroke, and respect `Animation` duration scale for Reduce Motion. Test on the lowest-spec tablet in the fleet.
- **State:** optimistic UI with reconciliation: action shows immediately, sync chip shows truth, roll back with a message on rejection.
- **Never block the UI thread** on network calls; timeouts 10 s with visible failure.

---

## 16. Decisions from your answers, and what is still open

**Decisions applied in v1.1**

| Your answer | Decision |
|---|---|
| Android tablet next to the POS | Viewing distance assumed 0.5–1.5 m, so the type scale stands, with 2 m kept as a safety margin. Because staff are at the till, the device competes with POS and customer attention: the header banner (8.1) must work on every screen and never rely on staff looking at the tablet |
| Device never prints (v1.2) | All print UI removed: no Print again, no Printed chip, no printer check in pre-flight, no printer setting, no bulk Print. Print-related copy and icons are banned from the UI |
| "Customer has arrived" events: unknown | **Not built in v1.** The Ready card reserves a 32 dp slot for an `Arrived` chip that is hidden when the field is absent, so adding it later is a no-code-change UI switch. Ask DirectDine one question: does the customer app have an "I'm here" action, and can it be webhooked? |
| Maybe more than one device | Multi-device rules added to 8.5. Design them in from day one; retrofitting shared state and shared silencing is expensive |
| No brand yet; portal is MOOG | The device is branded **MOOG** ("Maslows Online Gateway"). Use a neutral wordmark in the header left slot and pairing screen only. State colours are functional and must **not** be replaced by a brand colour. If you later pick a brand colour, it goes on the wordmark, the pairing screen and focus rings only, never on state colours |

**Still open**
1. Tablet model, screen size and Android version (sets the performance budget and whether lock-task, alarm-stream volume floor and boot-on-power work as specified).
2. Whether stores that run two devices want one to be display-only (a Ready board at the counter).
3. Whether any stores are unattended at opening (auto-accept mode needs its own alert ladder).
