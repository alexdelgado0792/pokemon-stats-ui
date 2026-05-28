// tests/PokemonOverlay.Tests/OverlayStateServiceTests.cs
using Xunit;
using PokemonOverlay.Models;
using PokemonOverlay.Services;

namespace PokemonOverlay.Tests;

public class OverlayStateServiceTests
{
    private static SlotPayload MakePayload(string name = "charizard") => new(
        new PokemonData(6, name, "Charizard", "fire", "flying",
            new Dictionary<string, StatValues>(),
            new Dictionary<string, string> { ["official-artwork"] = $"pokemon/official-artwork/{name}.png" },
            false, false),
        new NatureData("jolly", "Jolly", "speed", "special-attack"),
        "official-artwork",
        $"pokemon/official-artwork/{name}.png"
    );

    [Fact]
    public void InitialSnapshot_BothSlotsNull()
    {
        var svc = new OverlayStateService();
        var snap = svc.GetSnapshot();
        Assert.Null(snap.Left);
        Assert.Null(snap.Right);
    }

    [Fact]
    public void SetSlot_Left_UpdatesLeftSlot()
    {
        var svc = new OverlayStateService();
        var payload = MakePayload();
        svc.SetSlot("left", payload);
        Assert.Same(payload, svc.GetSnapshot().Left);
        Assert.Null(svc.GetSnapshot().Right);
    }

    [Fact]
    public void SetSlot_Right_UpdatesRightSlot()
    {
        var svc = new OverlayStateService();
        var payload = MakePayload();
        svc.SetSlot("right", payload);
        Assert.Null(svc.GetSnapshot().Left);
        Assert.Same(payload, svc.GetSnapshot().Right);
    }

    [Fact]
    public void ClearSlot_Left_SetsLeftToNull()
    {
        var svc = new OverlayStateService();
        svc.SetSlot("left", MakePayload());
        svc.ClearSlot("left");
        Assert.Null(svc.GetSnapshot().Left);
    }

    [Fact]
    public void ClearSlot_Right_SetsRightToNull()
    {
        var svc = new OverlayStateService();
        svc.SetSlot("right", MakePayload());
        svc.ClearSlot("right");
        Assert.Null(svc.GetSnapshot().Right);
    }

    [Fact]
    public void SetSlot_Left_DoesNotAffectRight()
    {
        var svc = new OverlayStateService();
        var rightPayload = MakePayload("squirtle");
        svc.SetSlot("right", rightPayload);
        svc.SetSlot("left",  MakePayload("charizard"));
        Assert.Same(rightPayload, svc.GetSnapshot().Right);
    }

    [Fact]
    public void ClearSlot_Left_DoesNotAffectRight()
    {
        var svc = new OverlayStateService();
        var rightPayload = MakePayload("squirtle");
        svc.SetSlot("right", rightPayload);
        svc.SetSlot("left",  MakePayload("charizard"));
        svc.ClearSlot("left");
        Assert.Same(rightPayload, svc.GetSnapshot().Right);
        Assert.Null(svc.GetSnapshot().Left);
    }
}
