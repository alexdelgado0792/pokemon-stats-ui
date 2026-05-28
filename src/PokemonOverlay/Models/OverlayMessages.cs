// src/PokemonOverlay/Models/OverlayMessages.cs
namespace PokemonOverlay.Models;

// REST request DTOs
public record SetSlotRequest(string Slot, string PokemonName, string NatureName, string SpriteVariant);
public record ClearSlotRequest(string Slot);

// WebSocket payload — sent inside slotUpdate and snapshot messages
public record SlotPayload(
    PokemonData Pokemon,
    NatureData  Nature,
    string      SpriteVariant,
    string      SpritePath
);

// Snapshot holds both slots (either can be null when empty)
public record SlotSnapshot(SlotPayload? Left, SlotPayload? Right);
