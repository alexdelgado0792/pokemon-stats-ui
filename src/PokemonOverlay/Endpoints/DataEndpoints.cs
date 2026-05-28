// src/PokemonOverlay/Endpoints/DataEndpoints.cs
using PokemonOverlay.Services;

namespace PokemonOverlay.Endpoints;

public static class DataEndpoints
{
    public static void MapDataEndpoints(this WebApplication app)
    {
        app.MapGet("/api/pokemon/search", (DataService data, string? q = null, int limit = 10) =>
        {
            if (string.IsNullOrWhiteSpace(q))
                return Results.Ok(Array.Empty<object>());
            return Results.Ok(data.Search(q, limit));
        });

        app.MapGet("/api/pokemon/{name}", (string name, DataService data) =>
        {
            var pokemon = data.GetByName(name);
            return pokemon is null ? Results.NotFound() : Results.Ok(pokemon);
        });

        app.MapGet("/api/natures", (DataService data) => Results.Ok(data.GetNatures()));

        app.MapGet("/api/items", (DataService data) => Results.Ok(data.GetItems()));
    }
}
