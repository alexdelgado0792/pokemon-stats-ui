// tests/PokemonOverlay.Tests/DataServiceTests.cs
using Xunit;
using PokemonOverlay.Models;
using PokemonOverlay.Services;

namespace PokemonOverlay.Tests;

public class DataServiceTests
{
    // Test dataset: bulbasaur(1), charmander(4), charizard(6), squirtle(7), pikachu(25)
    // "char" is a prefix of both charmander(4) and charizard(6)
    private static DataService MakeService() => new(
        new List<PokemonData>
        {
            new(1,  "bulbasaur",  "Bulbasaur",  "grass",    "poison",  new(), new(), false, false),
            new(4,  "charmander", "Charmander", "fire",     null,      new(), new(), false, false),
            new(6,  "charizard",  "Charizard",  "fire",     "flying",  new(), new(), false, false),
            new(7,  "squirtle",   "Squirtle",   "water",    null,      new(), new(), false, false),
            new(25, "pikachu",    "Pikachu",    "electric", null,      new(), new(), false, false),
        },
        new List<NatureData>
        {
            new("hardy",  "Hardy",  null,              null),
            new("jolly",  "Jolly",  "speed",           "special-attack"),
            new("modest", "Modest", "special-attack",  "attack"),
        },
        new List<ItemData>()
    );

    [Fact]
    public void Search_ExactName_ScoresHighest()
    {
        var svc = MakeService();
        Assert.Equal("charizard", svc.Search("charizard").First().Name);
    }

    [Fact]
    public void Search_PrefixTies_BrokenByIdAscending()
    {
        // "char" prefixes charmander(4) and charizard(6); charmander wins by lower id
        var svc = MakeService();
        var results = svc.Search("char").ToList();
        Assert.Equal("charmander", results[0].Name);
        Assert.Equal("charizard",  results[1].Name);
    }

    [Fact]
    public void Search_Exact_BeatsPrefix()
    {
        var svc = MakeService();
        Assert.Equal("charmander", svc.Search("charmander").First().Name);
    }

    [Fact]
    public void Search_Substring_Returned()
    {
        var svc = MakeService();
        Assert.Contains(svc.Search("saur"), p => p.Name == "bulbasaur");
    }

    [Fact]
    public void Search_CaseInsensitive()
    {
        var svc = MakeService();
        Assert.Equal("charizard", svc.Search("CHARIZARD").First().Name);
    }

    [Fact]
    public void Search_LimitRespected()
    {
        var svc = MakeService();
        Assert.Equal(2, svc.Search("a", limit: 2).Count());
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var svc = MakeService();
        Assert.Empty(svc.Search("missingno"));
    }

    [Fact]
    public void GetByName_ReturnsCorrectPokemon()
    {
        var svc = MakeService();
        var result = svc.GetByName("charizard");
        Assert.NotNull(result);
        Assert.Equal(6, result.Id);
    }

    [Fact]
    public void GetByName_CaseInsensitive()
    {
        var svc = MakeService();
        Assert.NotNull(svc.GetByName("Charizard"));
        Assert.NotNull(svc.GetByName("CHARIZARD"));
    }

    [Fact]
    public void GetByName_NotFound_ReturnsNull()
    {
        var svc = MakeService();
        Assert.Null(svc.GetByName("missingno"));
    }

    [Fact]
    public void GetNature_ReturnsMatch()
    {
        var svc = MakeService();
        Assert.NotNull(svc.GetNature("jolly"));
    }

    [Fact]
    public void GetNature_CaseInsensitive()
    {
        var svc = MakeService();
        Assert.NotNull(svc.GetNature("Jolly"));
        Assert.NotNull(svc.GetNature("JOLLY"));
    }

    [Fact]
    public void GetNature_NotFound_ReturnsNull()
    {
        var svc = MakeService();
        Assert.Null(svc.GetNature("nonexistent"));
    }

    [Fact]
    public void GetNatures_ReturnsAll()
    {
        var svc = MakeService();
        Assert.Equal(3, svc.GetNatures().Count);
    }
}
