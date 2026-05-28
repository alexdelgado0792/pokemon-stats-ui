// src/PokemonOverlay/Services/DataService.cs
using PokemonOverlay.Models;
using System.Text.Json;

namespace PokemonOverlay.Services;

public class DataService
{
    private readonly List<PokemonData> _pokemon;
    private readonly List<NatureData>  _natures;
    private readonly List<ItemData>    _items;

    private static readonly JsonSerializerOptions JsonOpts = new()
        { PropertyNameCaseInsensitive = true };

    // Production constructor — used by DI
    public DataService(IConfiguration config)
        : this(LoadAll(config)) { }

    // Testing constructor — no file I/O
    public DataService(List<PokemonData> pokemon, List<NatureData> natures, List<ItemData> items)
    {
        _pokemon = pokemon;
        _natures = natures;
        _items   = items;
    }

    private DataService((List<PokemonData>, List<NatureData>, List<ItemData>) data)
        : this(data.Item1, data.Item2, data.Item3) { }

    private static (List<PokemonData>, List<NatureData>, List<ItemData>) LoadAll(IConfiguration config)
    {
        var dir = config["OVERLAY_DATA_PATH"] ?? "wwwroot/data";
        return (
            Read<List<PokemonData>>(dir, "pokemon.json"),
            Read<List<NatureData>>(dir,  "natures.json"),
            Read<List<ItemData>>(dir,    "items.json")
        );
    }

    private static T Read<T>(string dir, string file) =>
        JsonSerializer.Deserialize<T>(
            File.ReadAllText(Path.Combine(dir, file)), JsonOpts)!;

    public IEnumerable<PokemonData> Search(string q, int limit = 10)
    {
        var lower = q.ToLowerInvariant();
        return _pokemon
            .Where(p => p.Name.Contains(lower, StringComparison.Ordinal)
                     || p.DisplayName.Contains(lower, StringComparison.OrdinalIgnoreCase))
            .Select(p => (p, Score(p, lower)))
            .OrderByDescending(x => x.Item2)
            .ThenBy(x => x.p.Id)
            .Take(limit)
            .Select(x => x.p);
    }

    private static int Score(PokemonData p, string lower)
    {
        if (p.Name.Equals(lower, StringComparison.OrdinalIgnoreCase) ||
            p.DisplayName.Equals(lower, StringComparison.OrdinalIgnoreCase))
            return 1000;
        if (p.Name.StartsWith(lower, StringComparison.OrdinalIgnoreCase) ||
            p.DisplayName.StartsWith(lower, StringComparison.OrdinalIgnoreCase))
            return 500;
        return 100;
    }

    public PokemonData? GetByName(string name) =>
        _pokemon.FirstOrDefault(p =>
            p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public NatureData? GetNature(string name) =>
        _natures.FirstOrDefault(n =>
            n.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    public IReadOnlyList<NatureData> GetNatures() => _natures;
    public IReadOnlyList<ItemData>   GetItems()   => _items;
}
