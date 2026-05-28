# PokemonStatsUI

## What this is
ASP.NET Core .NET 10 minimal API. Serves a Pokémon VGC stat overlay for OBS
plus a second-monitor control UI. Reads JSON data produced by PokemonDataImporter.

## Run commands
```bash
# Docker (from repo root) — normal workflow
docker compose up -d pokemon-overlay
docker compose logs -f pokemon-overlay
docker compose restart pokemon-overlay

# Native dev
cd src/PokemonOverlay
OVERLAY_DATA_PATH=$(pwd)/../../data dotnet run
```

## URLs
- Control UI (second monitor): http://localhost:5000/control
- OBS Browser Source:          http://localhost:5000/overlay
- Future: http://pokemon.overlay.local (via nginx, not yet configured)

## Env vars
| Variable           | Default        | Purpose                                      |
|--------------------|----------------|----------------------------------------------|
| OVERLAY_BIND_URL   | http://localhost:5000 | URL the web host binds to. Use http://0.0.0.0:5000 in Docker. |
| OVERLAY_DATA_PATH  | wwwroot/data   | Where pokemon.json + sprites live on disk.   |

## REST API
| Method | Path                              | Notes                                     |
|--------|-----------------------------------|-------------------------------------------|
| GET    | /api/pokemon/search?q=&limit=     | Substring search, prefix matches rank first |
| GET    | /api/pokemon/{name}               | Full Pokémon data                         |
| GET    | /api/natures                      | All 25 natures                            |
| GET    | /api/items                        | All hold items                            |
| POST   | /api/overlay/set                  | { slot, pokemonName, natureName, spriteVariant } |
| POST   | /api/overlay/clear                | { slot }                                  |
| WS     | /ws/overlay                       | Server-push state to overlay clients      |

Sprites are served at /data/sprites/{spritePath} via a secondary static file
middleware pointing to OVERLAY_DATA_PATH.

## WebSocket message shapes (server → overlay)
```json
{ "type": "snapshot",   "snapshot": { "left": SlotPayload|null, "right": SlotPayload|null } }
{ "type": "slotUpdate", "slot": "left"|"right", "data": SlotPayload }
{ "type": "slotClear",  "slot": "left"|"right" }
```
Snapshot is sent on initial connect so OBS restores state after a source reload.

## SlotPayload shape
```json
{
  "pokemon": { /* full PokemonData */ },
  "nature":  { "name": "jolly", "displayName": "Jolly", "increasedStat": "speed", "decreasedStat": "special-attack" },
  "spriteVariant": "official-artwork",
  "spritePath": "pokemon/official-artwork/charizard.png"
}
```

## Overlay UI (overlay.html / overlay.js / overlay.css)
- Two slots: left (Your Pokémon) and right (Opponent), side-by-side with VS between
- Each card: sprite, name, type pills, nature row, stat table (Label | Min | Base | Max)
- Nature-boosted stat row highlighted green, hindered stat row highlighted red
- Pokéball SVG spinner transition on slot update/clear (~500ms, fade 250ms)
- WebSocket auto-reconnects on close (1500ms backoff)
- Sprite URL: /data/sprites/${spritePath}

## Control UI (control.html / control.js / control.css)
- Two independent panels (left slot / right slot)
- Per panel: sprite variant dropdown, live search input (debounced 150ms),
  results list, nature dropdown (defaults to Hardy), Show button, Clear button
- Show button disabled until a Pokémon is selected
- Live indicator turns green when slot is active on overlay
- Nature dropdown shows modifier shorthand: "Jolly (+Spe / -SpA)"

## Source files
| File                            | Purpose                                        |
|---------------------------------|------------------------------------------------|
| Program.cs                      | Startup, routing, env var resolution           |
| Models/PokemonData.cs           | Mirrors pokemon.json / natures.json / items.json |
| Models/OverlayMessages.cs       | REST request DTOs + WebSocket message shapes   |
| Services/DataService.cs         | Loads JSON into memory at startup, search logic |
| Services/OverlayStateService.cs | Holds slot state, manages WS clients, broadcasts |
| Endpoints/DataEndpoints.cs      | GET routes                                     |
| Endpoints/OverlayEndpoints.cs   | POST routes + WebSocket upgrade                |
| wwwroot/overlay.*               | OBS browser source                             |
| wwwroot/control.*               | Second monitor control UI                      |

## Key decisions
- DataService is a singleton — data never changes at runtime
- OverlayStateService uses a lock for state mutation, broadcasts outside the lock
- Sprite variant fallback: if chosen variant is missing, picks any available one
- Search scoring: exact=1000, prefix=500, substring=100; ties broken by pokemon id
- wwwroot/data is only used for native dev; Docker reads from /data bind mount
