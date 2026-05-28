// src/PokemonOverlay/Endpoints/OverlayEndpoints.cs
using PokemonOverlay.Models;
using PokemonOverlay.Services;

namespace PokemonOverlay.Endpoints;

public static class OverlayEndpoints
{
    public static void MapOverlayEndpoints(this WebApplication app)
    {
        app.MapPost("/api/overlay/set", (SetSlotRequest req, DataService data, OverlayStateService state) =>
        {
            if (req.Slot != "left" && req.Slot != "right")
                return Results.BadRequest("slot must be 'left' or 'right'");

            var pokemon = data.GetByName(req.PokemonName);
            if (pokemon is null)
                return Results.NotFound($"Pokémon '{req.PokemonName}' not found");

            var nature = data.GetNature(req.NatureName);
            if (nature is null)
                return Results.NotFound($"Nature '{req.NatureName}' not found");

            // Sprite variant fallback: use any available variant if requested one is missing
            var variant = req.SpriteVariant;
            if (!pokemon.Sprites.ContainsKey(variant))
                variant = pokemon.Sprites.Keys.FirstOrDefault() ?? variant;

            var spritePath = pokemon.Sprites.GetValueOrDefault(variant, "");
            var payload    = new SlotPayload(pokemon, nature, variant, spritePath);

            state.SetSlot(req.Slot, payload);
            return Results.Ok(payload);
        });

        app.MapPost("/api/overlay/clear", (ClearSlotRequest req, OverlayStateService state) =>
        {
            if (req.Slot != "left" && req.Slot != "right")
                return Results.BadRequest("slot must be 'left' or 'right'");
            state.ClearSlot(req.Slot);
            return Results.Ok();
        });

        // WebSocket upgrade — app.Map accepts GET with Upgrade header
        app.Map("/ws/overlay", async (HttpContext ctx, OverlayStateService state) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            await state.HandleWebSocketAsync(ws, ctx.RequestAborted);
        });
    }
}
