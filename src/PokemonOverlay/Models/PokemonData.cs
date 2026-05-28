// src/PokemonOverlay/Models/PokemonData.cs
namespace PokemonOverlay.Models;

public record StatValues(int BaseStat, int Min, int Base, int Max);

public record PokemonData(
    int                            Id,
    string                         Name,
    string                         DisplayName,
    string                         PrimaryType,
    string?                        SecondaryType,
    Dictionary<string, StatValues> Stats,
    Dictionary<string, string>     Sprites,
    bool                           IsLegendary,
    bool                           IsMythical
);

public record NatureData(
    string  Name,
    string  DisplayName,
    string? IncreasedStat,
    string? DecreasedStat
);

public record ItemData(
    string Name,
    string DisplayName,
    string SpritePath
);
