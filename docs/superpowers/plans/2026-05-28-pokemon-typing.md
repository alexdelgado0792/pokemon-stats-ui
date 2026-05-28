# Pokémon Type Badge Images Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the text-based colored type pills in the overlay with the existing square PNG type badge images from `wwwroot/data/types/`.

**Architecture:** Two-file frontend-only change. `overlay.js` swaps the `<span>` type pill elements for `<img>` elements pointing to `/data/types/type_{name}_square.png` and removes the now-dead `TYPE_COLORS` constant. `overlay.css` removes the `.type-pill` rule and adds a `.type-badge` sizing rule. No server changes — the images are already served by the existing static files middleware.

**Tech Stack:** Vanilla JS, CSS, ASP.NET Core 10 static files (xUnit for .NET regression check)

---

### Task 1: Update `overlay.js` — swap type pills for badge images

**Files:**
- Modify: `src/PokemonOverlay/wwwroot/overlay.js:1-7` (remove `TYPE_COLORS`)
- Modify: `src/PokemonOverlay/wwwroot/overlay.js:41-44` (swap `<span>` to `<img>`)

- [ ] **Step 1: Delete the `TYPE_COLORS` constant**

Open `src/PokemonOverlay/wwwroot/overlay.js`. Remove lines 1–7 in their entirety:

```js
// DELETE these lines:
const TYPE_COLORS = {
  normal:'#A8A878',fire:'#F08030',water:'#6890F0',electric:'#F8D030',
  grass:'#78C850',ice:'#98D8D8',fighting:'#C03028',poison:'#A040A0',
  ground:'#E0C068',flying:'#A890F0',psychic:'#F85888',bug:'#A8B820',
  rock:'#B8A038',ghost:'#705898',dragon:'#7038F8',dark:'#705848',
  steel:'#B8B8D0',fairy:'#EE99AC',
};
```

- [ ] **Step 2: Replace the `typePills` mapping in `renderCard()`**

Find this block (currently around line 41–44 after the deletion above):

```js
  const types = [pokemon.primaryType, pokemon.secondaryType].filter(Boolean);
  const typePills = types.map(t =>
    `<span class="type-pill" style="background:${TYPE_COLORS[t] ?? '#888'}">${t}</span>`
  ).join('');
```

Replace it with:

```js
  const types = [pokemon.primaryType, pokemon.secondaryType].filter(Boolean);
  const typePills = types.map(t =>
    `<img class="type-badge" src="/data/types/type_${t}_square.png" alt="${t}">`
  ).join('');
```

- [ ] **Step 3: Verify no other references to `TYPE_COLORS` remain**

Run:
```
Select-String -Path "src\PokemonOverlay\wwwroot\overlay.js" -Pattern "TYPE_COLORS"
```
Expected: no output (zero matches).

- [ ] **Step 4: Commit**

```bash
git add src/PokemonOverlay/wwwroot/overlay.js
git commit -m "feat: replace type pills with badge images in overlay"
```

---

### Task 2: Update `overlay.css` — remove `.type-pill`, add `.type-badge`

**Files:**
- Modify: `src/PokemonOverlay/wwwroot/overlay.css:52-59` (remove `.type-pill` rule, add `.type-badge` rule)

- [ ] **Step 1: Remove the `.type-pill` rule**

Open `src/PokemonOverlay/wwwroot/overlay.css`. Find and delete the entire `.type-pill` block:

```css
/* DELETE this entire rule: */
.type-pill {
  border-radius: 4px;
  padding: 1px 6px;
  font-size: 11px;
  font-weight: 600;
  text-transform: capitalize;
  color: #fff;
  text-shadow: 0 1px 2px rgba(0,0,0,0.4);
}
```

- [ ] **Step 2: Add the `.type-badge` rule in its place**

In the same location (after the `.type-pills` container rule), add:

```css
.type-badge { height: 20px; width: auto; display: inline-block; }
```

The surrounding context should look like this after the change:

```css
.type-pills { display: flex; gap: 4px; flex-wrap: wrap; justify-content: center; }

.type-badge { height: 20px; width: auto; display: inline-block; }

.nature-row { font-size: 11px; opacity: 0.85; }
```

- [ ] **Step 3: Commit**

```bash
git add src/PokemonOverlay/wwwroot/overlay.css
git commit -m "feat: add type-badge CSS rule, remove type-pill"
```

---

### Task 3: Verify — run tests and check overlay in browser

**Files:** none modified

- [ ] **Step 1: Run the .NET test suite**

```bash
dotnet test tests/PokemonOverlay.Tests/PokemonOverlay.Tests.csproj
```

Expected: all tests pass. These tests cover `DataService` and `OverlayStateService` — both are unchanged, so this is a regression guard only.

- [ ] **Step 2: Start the dev server**

```bash
cd src/PokemonOverlay
dotnet run
```

Server starts at `http://localhost:5000`.

- [ ] **Step 3: Open the control panel and set a Pokémon**

Navigate to `http://localhost:5000/control`. Search for `charizard`, select it, pick any nature, click Show.

- [ ] **Step 4: Check the overlay**

Navigate to `http://localhost:5000/overlay` (or use a second tab).

Expected:
- The card for Charizard shows two type badge images (Fire and Flying square PNGs) in place of the old colored text pills.
- No broken image icons — the images load from `/data/types/type_fire_square.png` and `/data/types/type_flying_square.png`.
- The badges sit inline inside the `.type-pills` flex row, sized at 20px height.
- All other card content (sprite, name, nature row, stat table) is unchanged.

- [ ] **Step 5: Test a single-type Pokémon**

Search for `pikachu` (Electric, no secondary type). Confirm only one badge image appears.

- [ ] **Step 6: Test a clear**

Click Clear on the control panel. Confirm the Pokéball animation plays and the card empties normally.
