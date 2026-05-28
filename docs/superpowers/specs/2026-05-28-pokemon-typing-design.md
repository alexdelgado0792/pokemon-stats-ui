# Pokémon Type Badge Images in Overlay

**Date:** 2026-05-28
**Scope:** Overlay UI only (`overlay.js`, `overlay.css`)

## Problem

The overlay currently renders Pokémon type pills as `<span>` elements with an inline CSS background color drawn from a hardcoded `TYPE_COLORS` map. The `wwwroot/data/types/` directory already contains 18 square PNG badge images (e.g. `type_fire_square.png`) that match the in-game type icons. These should be used instead.

## Goal

Replace the text-based colored type pills in the overlay cards with the existing square type badge PNG images.

## Design

### `overlay.js`

- In `renderCard()`, change the `typePills` mapping:
  - **Before:** `<span class="type-pill" style="background:${TYPE_COLORS[t] ?? '#888'}">${t}</span>`
  - **After:** `<img class="type-badge" src="/data/types/type_${t}_square.png" alt="${t}">`
- Delete the `TYPE_COLORS` constant — it is no longer referenced anywhere.

### `overlay.css`

- Remove the `.type-pill` rule (background, padding, border-radius, text-shadow, text-transform, color).
- Add a `.type-badge` rule sized to fit the existing `.type-pills` flex row:
  ```css
  .type-badge { height: 20px; width: auto; display: inline-block; }
  ```

### No other changes

- The `.type-pills` flex container in both JS and CSS is unchanged.
- The server, WebSocket flow, `SlotPayload`, and control panel are untouched.

## Data

The PNG images live at `wwwroot/data/types/type_{name}_square.png` and are served at `/data/types/type_{name}_square.png` via the existing static files middleware. Type name strings from the API (e.g. `"fire"`, `"water"`) match the filename pattern exactly — no mapping required.

All 18 Pokémon types have a corresponding PNG in the directory.

## Out of scope

- Control panel search results (no type display there).
- Error/fallback handling for missing type images (all types are covered).
- Any server-side changes.
