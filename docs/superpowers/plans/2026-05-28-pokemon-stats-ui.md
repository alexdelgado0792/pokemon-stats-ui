# PokemonStatsUI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [x]`) syntax for tracking.

**Goal:** Build an ASP.NET Core .NET 10 minimal API that serves a live Pokémon VGC stat overlay for OBS and a second-monitor control panel, with real-time slot updates pushed over WebSocket.

**Architecture:** A singleton DataService loads JSON files from disk at startup. A singleton OverlayStateService holds the two slot states, manages WebSocket clients, and broadcasts updates. All routes are minimal API endpoints. Frontend is plain HTML/JS/CSS served from wwwroot.

**Tech Stack:** ASP.NET Core .NET 10 minimal API, System.Text.Json, plain WebSocket (no SignalR), xUnit for unit tests, vanilla JS frontend.

---

## File Map

**Create:**
- `src/PokemonOverlay/PokemonOverlay.csproj`
- `src/PokemonOverlay/Program.cs`
- `src/PokemonOverlay/Models/PokemonData.cs`
- `src/PokemonOverlay/Models/OverlayMessages.cs`
- `src/PokemonOverlay/Services/DataService.cs`
- `src/PokemonOverlay/Services/OverlayStateService.cs`
- `src/PokemonOverlay/Endpoints/DataEndpoints.cs`
- `src/PokemonOverlay/Endpoints/OverlayEndpoints.cs`
- `src/PokemonOverlay/wwwroot/overlay.html`
- `src/PokemonOverlay/wwwroot/overlay.js`
- `src/PokemonOverlay/wwwroot/overlay.css`
- `src/PokemonOverlay/wwwroot/control.html`
- `src/PokemonOverlay/wwwroot/control.js`
- `src/PokemonOverlay/wwwroot/control.css`
- `tests/PokemonOverlay.Tests/PokemonOverlay.Tests.csproj`
- `tests/PokemonOverlay.Tests/DataServiceTests.cs`
- `tests/PokemonOverlay.Tests/OverlayStateServiceTests.cs`

---

## Task 1: Project Scaffolding

**Files:**
- Create: `src/PokemonOverlay/PokemonOverlay.csproj`
- Create: `tests/PokemonOverlay.Tests/PokemonOverlay.Tests.csproj`

- [x] **Step 1: Create the main project file**

```xml
<!-- src/PokemonOverlay/PokemonOverlay.csproj -->
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <RootNamespace>PokemonOverlay</RootNamespace>
  </PropertyGroup>
</Project>
```

- [x] **Step 2: Create a minimal Program.cs so the project compiles**

```csharp
// src/PokemonOverlay/Program.cs
var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
app.Run();
```

- [x] **Step 3: Create the test project file**

```xml
<!-- tests/PokemonOverlay.Tests/PokemonOverlay.Tests.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.*" />
    <PackageReference Include="xunit" Version="2.*" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.*" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\..\src\PokemonOverlay\PokemonOverlay.csproj" />
  </ItemGroup>
</Project>
```

- [x] **Step 4: Build both projects**

```
dotnet build src/PokemonOverlay/PokemonOverlay.csproj
dotnet build tests/PokemonOverlay.Tests/PokemonOverlay.Tests.csproj
```

Expected: 0 errors each.

- [x] **Step 5: Commit**

```bash
git add CLAUDE.md src/PokemonOverlay/ tests/PokemonOverlay.Tests/ docs/
git commit -m "chore: scaffold ASP.NET Core project and test project"
```

---

## Task 2: Data Models

**Files:**
- Create: `src/PokemonOverlay/Models/PokemonData.cs`
- Create: `src/PokemonOverlay/Models/OverlayMessages.cs`

These records mirror the JSON shapes produced by the importer. No tests — pure data containers.

- [x] **Step 1: Create Models/PokemonData.cs**

```csharp
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
```

- [x] **Step 2: Create Models/OverlayMessages.cs**

```csharp
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

// WebSocket snapshot message
public record SlotSnapshot(SlotPayload? Left, SlotPayload? Right);
```

- [x] **Step 3: Build to verify**

```
dotnet build src/PokemonOverlay/PokemonOverlay.csproj
```

Expected: 0 errors.

- [x] **Step 4: Commit**

```bash
git add src/PokemonOverlay/Models/
git commit -m "feat: add data models for pokemon, natures, items, and overlay messages"
```

---

## Task 3: DataService (TDD)

**Files:**
- Create: `src/PokemonOverlay/Services/DataService.cs`
- Create: `tests/PokemonOverlay.Tests/DataServiceTests.cs`

DataService is a singleton that loads all JSON data at startup and exposes search and lookup methods.

**Search scoring rules (case-insensitive, matches against name and displayName):**
- exact match → 1000
- prefix match → 500
- substring match → 100
- ties broken by pokemon id (ascending)

- [x] **Step 1: Write the failing tests**

```csharp
// tests/PokemonOverlay.Tests/DataServiceTests.cs
using Xunit;
using PokemonOverlay.Models;
using PokemonOverlay.Services;

namespace PokemonOverlay.Tests;

public class DataServiceTests
{
    // Test dataset: bulbasaur(1), charmander(4), charizard(6), squirtle(7), pikachu(25)
    // "char" is a prefix of charmander AND charizard
    // "saur" is a substring of bulbasaur
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
            new("hardy",  "Hardy",  null,    null),
            new("jolly",  "Jolly",  "speed", "special-attack"),
            new("modest", "Modest", "special-attack", "attack"),
        },
        new List<ItemData>()
    );

    [Fact]
    public void Search_ExactName_Scores_Highest()
    {
        var svc = MakeService();
        var results = svc.Search("charizard").ToList();
        Assert.Equal("charizard", results[0].Name);
    }

    [Fact]
    public void Search_PrefixTies_Broken_By_Id()
    {
        // "char" is a prefix of both charmander(4) and charizard(6) — charmander wins by id
        var svc = MakeService();
        var results = svc.Search("char").ToList();
        Assert.Equal("charmander", results[0].Name);
        Assert.Equal("charizard",  results[1].Name);
    }

    [Fact]
    public void Search_Exact_Beats_Prefix()
    {
        // "charmander" exactly matches charmander; "charizard" is a prefix of nothing else
        var svc = MakeService();
        var results = svc.Search("charmander").ToList();
        Assert.Equal("charmander", results[0].Name);
    }

    [Fact]
    public void Search_Substring_Returned()
    {
        // "saur" is a substring of bulbasaur
        var svc = MakeService();
        var results = svc.Search("saur").ToList();
        Assert.Contains(results, p => p.Name == "bulbasaur");
    }

    [Fact]
    public void Search_CaseInsensitive()
    {
        var svc = MakeService();
        var results = svc.Search("CHARIZARD").ToList();
        Assert.Equal("charizard", results[0].Name);
    }

    [Fact]
    public void Search_Limit_Respected()
    {
        var svc = MakeService();
        var results = svc.Search("a", limit: 2).ToList();
        Assert.Equal(2, results.Count);
    }

    [Fact]
    public void Search_NoMatch_ReturnsEmpty()
    {
        var svc = MakeService();
        Assert.Empty(svc.Search("missingno"));
    }

    [Fact]
    public void GetByName_ReturnsMatch()
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
```

- [x] **Step 2: Run tests — verify they fail**

```
dotnet test tests/PokemonOverlay.Tests/PokemonOverlay.Tests.csproj
```

Expected: build error — `DataService` does not exist yet.

- [x] **Step 3: Create Services/DataService.cs**

```csharp
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

    // Production constructor — called by DI
    public DataService(IConfiguration config)
        : this(Load(config)) { }

    // Testing constructor — pass data directly, no file I/O
    public DataService(
        List<PokemonData> pokemon,
        List<NatureData>  natures,
        List<ItemData>    items)
    {
        _pokemon = pokemon;
        _natures = natures;
        _items   = items;
    }

    private DataService((List<PokemonData>, List<NatureData>, List<ItemData>) data)
        : this(data.Item1, data.Item2, data.Item3) { }

    private static (List<PokemonData>, List<NatureData>, List<ItemData>) Load(IConfiguration config)
    {
        var dir = config["OVERLAY_DATA_PATH"] ?? "wwwroot/data";
        return (
            Read<List<PokemonData>>(dir, "pokemon.json"),
            Read<List<NatureData>>(dir, "natures.json"),
            Read<List<ItemData>>(dir, "items.json")
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
        if (p.Name == lower ||
            p.DisplayName.Equals(lower, StringComparison.OrdinalIgnoreCase))
            return 1000;
        if (p.Name.StartsWith(lower, StringComparison.Ordinal) ||
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
```

- [x] **Step 4: Run tests — verify they pass**

```
dotnet test tests/PokemonOverlay.Tests/PokemonOverlay.Tests.csproj
```

Expected: 14 tests pass, 0 fail.

- [x] **Step 5: Commit**

```bash
git add src/PokemonOverlay/Services/DataService.cs tests/PokemonOverlay.Tests/DataServiceTests.cs
git commit -m "feat: add DataService with search scoring and TDD tests"
```

---

## Task 4: OverlayStateService (TDD)

**Files:**
- Create: `src/PokemonOverlay/Services/OverlayStateService.cs`
- Create: `tests/PokemonOverlay.Tests/OverlayStateServiceTests.cs`

OverlayStateService holds the two slot states (left/right), manages WebSocket client connections, and broadcasts updates. State mutation uses a lock; broadcasts happen outside the lock.

- [x] **Step 1: Write the failing tests**

```csharp
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
        var snap = svc.GetSnapshot();
        Assert.Same(payload, snap.Left);
        Assert.Null(snap.Right);
    }

    [Fact]
    public void SetSlot_Right_UpdatesRightSlot()
    {
        var svc = new OverlayStateService();
        var payload = MakePayload();
        svc.SetSlot("right", payload);
        var snap = svc.GetSnapshot();
        Assert.Null(snap.Left);
        Assert.Same(payload, snap.Right);
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
        svc.SetSlot("left", MakePayload("charizard"));
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
```

- [x] **Step 2: Run tests — verify they fail**

```
dotnet test tests/PokemonOverlay.Tests/PokemonOverlay.Tests.csproj
```

Expected: build error — `OverlayStateService` does not exist yet.

- [x] **Step 3: Create Services/OverlayStateService.cs**

```csharp
// src/PokemonOverlay/Services/OverlayStateService.cs
using PokemonOverlay.Models;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace PokemonOverlay.Services;

public class OverlayStateService
{
    private readonly object _lock = new();
    private SlotPayload? _left;
    private SlotPayload? _right;

    private readonly ConcurrentDictionary<Guid, WebSocket> _clients = new();

    private static readonly JsonSerializerOptions JsonOpts = new()
        { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public void SetSlot(string slot, SlotPayload payload)
    {
        object msg;
        lock (_lock)
        {
            if (slot == "left") _left  = payload;
            else                _right = payload;
            msg = new { type = "slotUpdate", slot, data = payload };
        }
        _ = BroadcastAsync(msg);
    }

    public void ClearSlot(string slot)
    {
        object msg;
        lock (_lock)
        {
            if (slot == "left") _left  = null;
            else                _right = null;
            msg = new { type = "slotClear", slot };
        }
        _ = BroadcastAsync(msg);
    }

    public SlotSnapshot GetSnapshot()
    {
        lock (_lock) return new SlotSnapshot(_left, _right);
    }

    public async Task HandleWebSocketAsync(WebSocket ws)
    {
        var id = Guid.NewGuid();
        _clients[id] = ws;
        try
        {
            // Send current state immediately on connect so OBS restores after reload
            await SendAsync(ws, new { type = "snapshot", snapshot = GetSnapshot() });

            // Read loop — overlay clients never send data; we just detect disconnect
            var buffer = new byte[128];
            var result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            while (result.MessageType != WebSocketMessageType.Close
                   && ws.State == WebSocketState.Open)
            {
                result = await ws.ReceiveAsync(buffer, CancellationToken.None);
            }
            if (ws.State == WebSocketState.CloseReceived)
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "", CancellationToken.None);
        }
        finally
        {
            _clients.TryRemove(id, out _);
        }
    }

    private async Task BroadcastAsync(object message)
    {
        var bytes   = Encode(message);
        var segment = new ArraySegment<byte>(bytes);
        foreach (var (id, client) in _clients)
        {
            if (client.State != WebSocketState.Open)
            {
                _clients.TryRemove(id, out _);
                continue;
            }
            try
            {
                await client.SendAsync(segment, WebSocketMessageType.Text, true, CancellationToken.None);
            }
            catch
            {
                _clients.TryRemove(id, out _);
            }
        }
    }

    private static Task SendAsync(WebSocket ws, object message) =>
        ws.SendAsync(new ArraySegment<byte>(Encode(message)),
            WebSocketMessageType.Text, true, CancellationToken.None);

    private static byte[] Encode(object message) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOpts));
}
```

- [x] **Step 4: Run tests — verify they pass**

```
dotnet test tests/PokemonOverlay.Tests/PokemonOverlay.Tests.csproj
```

Expected: 21 tests pass (14 from Task 3 + 7 new), 0 fail.

- [x] **Step 5: Commit**

```bash
git add src/PokemonOverlay/Services/OverlayStateService.cs tests/PokemonOverlay.Tests/OverlayStateServiceTests.cs
git commit -m "feat: add OverlayStateService with slot state, WS client management, and tests"
```

---

## Task 5: Endpoints + Program.cs

**Files:**
- Create: `src/PokemonOverlay/Endpoints/DataEndpoints.cs`
- Create: `src/PokemonOverlay/Endpoints/OverlayEndpoints.cs`
- Modify: `src/PokemonOverlay/Program.cs`

Wire everything together. After this task the full API is live and testable with curl.

- [x] **Step 1: Create Endpoints/DataEndpoints.cs**

```csharp
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
```

- [x] **Step 2: Create Endpoints/OverlayEndpoints.cs**

```csharp
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

            // Sprite variant fallback: use any available variant if the requested one is missing
            var variant = req.SpriteVariant;
            if (!pokemon.Sprites.ContainsKey(variant))
                variant = pokemon.Sprites.Keys.FirstOrDefault() ?? variant;

            var spritePath = pokemon.Sprites.GetValueOrDefault(variant, "");
            var payload = new SlotPayload(pokemon, nature, variant, spritePath);

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

        // WebSocket upgrade — app.Map accepts the HTTP GET with Upgrade header
        app.Map("/ws/overlay", async (HttpContext ctx, OverlayStateService state) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
                return;
            }
            var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            await state.HandleWebSocketAsync(ws);
        });
    }
}
```

- [x] **Step 3: Replace Program.cs with the full wired-up app**

```csharp
// src/PokemonOverlay/Program.cs
using Microsoft.Extensions.FileProviders;
using PokemonOverlay.Endpoints;
using PokemonOverlay.Services;

var builder = WebApplication.CreateBuilder(args);

// Env var resolution
var bindUrl  = Environment.GetEnvironmentVariable("OVERLAY_BIND_URL")  ?? "http://localhost:5000";
var dataPath = Environment.GetEnvironmentVariable("OVERLAY_DATA_PATH") ?? "wwwroot/data";

builder.WebHost.UseUrls(bindUrl);
builder.Configuration["OVERLAY_DATA_PATH"] = dataPath;

builder.Services.AddSingleton<DataService>();
builder.Services.AddSingleton<OverlayStateService>();

var app = builder.Build();

app.UseWebSockets();

// Primary static files: wwwroot/ → JS, CSS, HTML
app.UseStaticFiles();

// Secondary static files: OVERLAY_DATA_PATH → /data (sprites, JSON)
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(dataPath)),
    RequestPath  = "/data",
});

// Named routes for the two UIs (URLs without .html extension per spec)
app.MapGet("/overlay", (IWebHostEnvironment env) =>
    Results.File(Path.Combine(env.WebRootPath, "overlay.html"), "text/html"));

app.MapGet("/control", (IWebHostEnvironment env) =>
    Results.File(Path.Combine(env.WebRootPath, "control.html"), "text/html"));

app.MapDataEndpoints();
app.MapOverlayEndpoints();

app.Run();
```

- [x] **Step 4: Build**

```
dotnet build src/PokemonOverlay/PokemonOverlay.csproj
```

Expected: 0 errors.

- [x] **Step 5: Run and smoke-test the API**

First, you need JSON data files. The importer (sibling repo) produces them. For a quick smoke test, create a minimal `wwwroot/data/` with small JSON files:

```bash
mkdir -p src/PokemonOverlay/wwwroot/data
echo '[{"id":6,"name":"charizard","displayName":"Charizard","primaryType":"fire","secondaryType":"flying","stats":{"hp":{"baseStat":78,"min":138,"base":153,"max":185},"speed":{"baseStat":100,"min":94,"base":120,"max":167}},"sprites":{"official-artwork":"pokemon/official-artwork/charizard.png"},"isLegendary":false,"isMythical":false}]' > src/PokemonOverlay/wwwroot/data/pokemon.json
echo '[{"name":"hardy","displayName":"Hardy","increasedStat":null,"decreasedStat":null},{"name":"jolly","displayName":"Jolly","increasedStat":"speed","decreasedStat":"special-attack"}]' > src/PokemonOverlay/wwwroot/data/natures.json
echo '[]' > src/PokemonOverlay/wwwroot/data/items.json
```

Then run from the project directory:
```bash
cd src/PokemonOverlay && dotnet run
```

Test the endpoints (in a new terminal):
```bash
# Search
curl "http://localhost:5000/api/pokemon/search?q=char"
# Expected: JSON array containing charizard

# By name
curl "http://localhost:5000/api/pokemon/charizard"
# Expected: full charizard JSON

# Natures
curl "http://localhost:5000/api/natures"
# Expected: JSON array with hardy and jolly

# Set slot
curl -X POST http://localhost:5000/api/overlay/set \
  -H "Content-Type: application/json" \
  -d '{"slot":"left","pokemonName":"charizard","natureName":"jolly","spriteVariant":"official-artwork"}'
# Expected: 200 with SlotPayload JSON

# Clear slot
curl -X POST http://localhost:5000/api/overlay/clear \
  -H "Content-Type: application/json" \
  -d '{"slot":"left"}'
# Expected: 200
```

All 5 commands should return 200 with valid JSON. Stop the server when done.

- [x] **Step 6: Run all tests**

```
dotnet test tests/PokemonOverlay.Tests/PokemonOverlay.Tests.csproj
```

Expected: 21 tests pass.

- [x] **Step 7: Commit**

```bash
git add src/PokemonOverlay/Endpoints/ src/PokemonOverlay/Program.cs
git commit -m "feat: add REST and WebSocket endpoints, wire up DI and static files"
```

---

## Task 6: Overlay UI

**Files:**
- Create: `src/PokemonOverlay/wwwroot/overlay.html`
- Create: `src/PokemonOverlay/wwwroot/overlay.js`
- Create: `src/PokemonOverlay/wwwroot/overlay.css`

The overlay connects via WebSocket and renders two Pokémon cards side-by-side. It handles snapshot (restore on reconnect), slotUpdate (animate in new data), and slotClear (animate out).

**Pokéball transition:** on each slot change, show spinning Pokéball for 500ms then fade in updated card content over 250ms.

**Type colors** used in pills — standard Pokémon type palette:
```
normal:#A8A878  fire:#F08030    water:#6890F0  electric:#F8D030
grass:#78C850   ice:#98D8D8     fighting:#C03028  poison:#A040A0
ground:#E0C068  flying:#A890F0  psychic:#F85888   bug:#A8B820
rock:#B8A038    ghost:#705898   dragon:#7038F8    dark:#705848
steel:#B8B8D0   fairy:#EE99AC
```

**Nature → stat key mapping** (natures use PokeAPI identifiers, stats dict uses camelCase):
```
hp → hp | attack → attack | defense → defense
special-attack → specialAttack | special-defense → specialDefense | speed → speed
```

- [x] **Step 1: Create wwwroot/overlay.html**

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>Pokémon Overlay</title>
  <link rel="stylesheet" href="/overlay.css">
</head>
<body>
  <div id="overlay">
    <div class="slot-wrap" id="slot-left">
      <div class="slot-label">Your Pokémon</div>
      <div class="card" id="card-left"></div>
    </div>
    <div id="vs">VS</div>
    <div class="slot-wrap" id="slot-right">
      <div class="slot-label">Opponent</div>
      <div class="card" id="card-right"></div>
    </div>
  </div>
  <script src="/overlay.js"></script>
</body>
</html>
```

- [x] **Step 2: Create wwwroot/overlay.js**

```javascript
// overlay.js
const TYPE_COLORS = {
  normal:'#A8A878',fire:'#F08030',water:'#6890F0',electric:'#F8D030',
  grass:'#78C850',ice:'#98D8D8',fighting:'#C03028',poison:'#A040A0',
  ground:'#E0C068',flying:'#A890F0',psychic:'#F85888',bug:'#A8B820',
  rock:'#B8A038',ghost:'#705898',dragon:'#7038F8',dark:'#705848',
  steel:'#B8B8D0',fairy:'#EE99AC',
};

// Maps PokeAPI stat identifiers (used in NatureData) to stats dict keys (used in PokemonData.stats)
const NATURE_TO_STAT_KEY = {
  'hp':'hp','attack':'attack','defense':'defense',
  'special-attack':'specialAttack','special-defense':'specialDefense','speed':'speed',
};

const STATS = [
  { key:'hp',            label:'HP'  },
  { key:'attack',        label:'Atk' },
  { key:'defense',       label:'Def' },
  { key:'specialAttack', label:'SpA' },
  { key:'specialDefense',label:'SpD' },
  { key:'speed',         label:'Spe' },
];

const POKEBALL_SVG = `
<svg class="pokeball" viewBox="0 0 44 44" xmlns="http://www.w3.org/2000/svg">
  <path d="M22 2A20 20 0 0 1 42 22H2A20 20 0 0 1 22 2Z" fill="#e53935"/>
  <rect x="2" y="20" width="40" height="4" fill="white"/>
  <path d="M22 42A20 20 0 0 1 2 22H42A20 20 0 0 1 22 42Z" fill="white"/>
  <circle cx="22" cy="22" r="5" fill="white" stroke="#333" stroke-width="2"/>
</svg>`;

function renderCard(payload) {
  if (!payload) return '<div class="empty">—</div>';
  const { pokemon, nature, spritePath } = payload;

  const boostedKey  = nature.increasedStat ? NATURE_TO_STAT_KEY[nature.increasedStat]  : null;
  const hinderedKey = nature.decreasedStat ? NATURE_TO_STAT_KEY[nature.decreasedStat] : null;

  const types = [pokemon.primaryType, pokemon.secondaryType].filter(Boolean);
  const typePills = types.map(t =>
    `<span class="type-pill" style="background:${TYPE_COLORS[t] ?? '#888'}">${t}</span>`
  ).join('');

  const natureRow = nature.increasedStat
    ? `${nature.displayName} <span class="nature-boost">+${nature.increasedStat}</span> / <span class="nature-hinder">-${nature.decreasedStat}</span>`
    : nature.displayName;

  const statRows = STATS.map(({ key, label }) => {
    const sv = pokemon.stats[key];
    if (!sv) return '';
    const cls = key === boostedKey ? 'class="boosted"'
              : key === hinderedKey ? 'class="hindered"'
              : '';
    return `<tr ${cls}><td>${label}</td><td>${sv.min}</td><td>${sv.base}</td><td>${sv.max}</td></tr>`;
  }).join('');

  return `
    <img class="sprite" src="/data/sprites/${spritePath}" alt="${pokemon.displayName}">
    <div class="pokemon-name">${pokemon.displayName}</div>
    <div class="type-pills">${typePills}</div>
    <div class="nature-row">${natureRow}</div>
    <table class="stat-table">
      <thead><tr><th></th><th>Min</th><th>Base</th><th>Max</th></tr></thead>
      <tbody>${statRows}</tbody>
    </table>`;
}

function animateSlot(slotId, payload) {
  const card = document.getElementById(slotId === 'left' ? 'card-left' : 'card-right');
  card.classList.add('spinning');
  card.innerHTML = POKEBALL_SVG;

  setTimeout(() => {
    card.classList.remove('spinning');
    card.classList.add('fading-in');
    card.innerHTML = renderCard(payload);
    setTimeout(() => card.classList.remove('fading-in'), 250);
  }, 500);
}

function applySnapshot(snapshot) {
  for (const slot of ['left', 'right']) {
    const card = document.getElementById(`card-${slot}`);
    card.innerHTML = renderCard(snapshot[slot]);
  }
}

function connect() {
  const ws = new WebSocket(`ws://${location.host}/ws/overlay`);

  ws.onmessage = (e) => {
    const msg = JSON.parse(e.data);
    if (msg.type === 'snapshot') {
      applySnapshot(msg.snapshot);
    } else if (msg.type === 'slotUpdate') {
      animateSlot(msg.slot, msg.data);
    } else if (msg.type === 'slotClear') {
      animateSlot(msg.slot, null);
    }
  };

  ws.onclose = () => setTimeout(connect, 1500);
}

connect();
```

- [x] **Step 3: Create wwwroot/overlay.css**

```css
/* overlay.css */
* { box-sizing: border-box; margin: 0; padding: 0; }

body {
  background: transparent;
  font-family: 'Segoe UI', system-ui, sans-serif;
  font-size: 13px;
  color: #fff;
}

#overlay {
  display: flex;
  align-items: flex-start;
  gap: 12px;
  padding: 8px;
}

.slot-wrap { display: flex; flex-direction: column; align-items: center; }

.slot-label {
  font-size: 11px;
  text-transform: uppercase;
  letter-spacing: 1px;
  opacity: 0.7;
  margin-bottom: 4px;
}

.card {
  background: rgba(20, 20, 40, 0.85);
  border-radius: 10px;
  padding: 10px;
  width: 200px;
  min-height: 220px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
}

.empty { opacity: 0.3; margin: auto; font-size: 18px; }

.sprite {
  width: 96px;
  height: 96px;
  object-fit: contain;
  image-rendering: pixelated;
}

.pokemon-name { font-size: 16px; font-weight: bold; }

.type-pills { display: flex; gap: 4px; flex-wrap: wrap; justify-content: center; }

.type-pill {
  border-radius: 4px;
  padding: 1px 6px;
  font-size: 11px;
  font-weight: 600;
  text-transform: capitalize;
  color: #fff;
  text-shadow: 0 1px 2px rgba(0,0,0,0.4);
}

.nature-row { font-size: 11px; opacity: 0.85; }
.nature-boost  { color: #66bb6a; font-weight: bold; }
.nature-hinder { color: #ef5350; font-weight: bold; }

.stat-table { width: 100%; border-collapse: collapse; font-size: 12px; margin-top: 4px; }
.stat-table th, .stat-table td {
  padding: 2px 4px;
  text-align: center;
}
.stat-table th { opacity: 0.6; font-weight: normal; }
.stat-table td:first-child { text-align: left; opacity: 0.8; }

.stat-table tr.boosted  td { background: rgba(102,187,106,0.25); color: #a5d6a7; }
.stat-table tr.hindered td { background: rgba(239, 83, 80,0.25); color: #ef9a9a; }

#vs {
  font-size: 28px;
  font-weight: 900;
  align-self: center;
  opacity: 0.6;
  text-shadow: 0 2px 6px rgba(0,0,0,0.5);
}

/* Pokéball spinner */
.pokeball {
  width: 60px;
  height: 60px;
  margin: auto;
  display: block;
}
.spinning .pokeball {
  animation: spin 0.4s linear infinite;
}
@keyframes spin { to { transform: rotate(360deg); } }

/* Fade-in after spin */
.fading-in { animation: fadeIn 0.25s ease-in; }
@keyframes fadeIn { from { opacity: 0; } to { opacity: 1; } }
```

- [x] **Step 4: Start the server and open the overlay in a browser**

```bash
cd src/PokemonOverlay && dotnet run
```

Open http://localhost:5000/overlay in a browser.
Expected: Two empty slots (showing "—") side-by-side with VS between them.

- [x] **Step 5: Test a slot update**

In a second terminal:
```bash
curl -X POST http://localhost:5000/api/overlay/set \
  -H "Content-Type: application/json" \
  -d '{"slot":"left","pokemonName":"charizard","natureName":"jolly","spriteVariant":"official-artwork"}'
```

Expected in browser: Pokéball spins for ~500ms, then Charizard card fades in with fire/flying type pills, speed row highlighted green, stat table shown.

- [x] **Step 6: Test slot clear**

```bash
curl -X POST http://localhost:5000/api/overlay/clear \
  -H "Content-Type: application/json" \
  -d '{"slot":"left"}'
```

Expected: Pokéball spin, then card returns to empty "—" state.

- [x] **Step 7: Test WebSocket reconnect**

Restart the server while the browser tab is open.
Expected: Overlay reconnects automatically within ~1.5s and restores state (if any was set before restart, it will be blank — that's correct, in-memory state resets on restart).

- [x] **Step 8: Commit**

```bash
git add src/PokemonOverlay/wwwroot/overlay.html src/PokemonOverlay/wwwroot/overlay.js src/PokemonOverlay/wwwroot/overlay.css
git commit -m "feat: add OBS overlay UI with WebSocket, stat cards, and Pokéball transition"
```

---

## Task 7: Control UI

**Files:**
- Create: `src/PokemonOverlay/wwwroot/control.html`
- Create: `src/PokemonOverlay/wwwroot/control.js`
- Create: `src/PokemonOverlay/wwwroot/control.css`

Two-panel control interface. Each panel has: sprite variant dropdown, live search (150ms debounce), results list, nature dropdown, Show/Clear buttons, live indicator.

**Nature dropdown format:** `"Jolly (+Spe / -SpA)"` for natures with modifiers, `"Hardy"` for neutral natures.

**Stat abbreviations:**
```javascript
{ hp:'HP', attack:'Atk', defense:'Def',
  'special-attack':'SpA', 'special-defense':'SpD', speed:'Spe' }
```

**Live indicator:** connects to `/ws/overlay` and turns green when the slot's snapshot shows non-null.

- [x] **Step 1: Create wwwroot/control.html**

```html
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <title>Pokémon Control</title>
  <link rel="stylesheet" href="/control.css">
</head>
<body>
  <div id="app">
    <div class="panel" data-slot="left">
      <h2>Left Slot <span class="indicator" id="ind-left"></span></h2>
      <label>Sprite variant
        <select class="variant-select">
          <option value="official-artwork">Official Artwork</option>
          <option value="home">Home</option>
          <option value="default">Default</option>
        </select>
      </label>
      <input type="search" class="search-input" placeholder="Search Pokémon…">
      <ul class="results-list"></ul>
      <label>Nature
        <select class="nature-select"></select>
      </label>
      <div class="actions">
        <button class="btn-show" disabled>Show</button>
        <button class="btn-clear">Clear</button>
      </div>
    </div>

    <div class="panel" data-slot="right">
      <h2>Right Slot <span class="indicator" id="ind-right"></span></h2>
      <label>Sprite variant
        <select class="variant-select">
          <option value="official-artwork">Official Artwork</option>
          <option value="home">Home</option>
          <option value="default">Default</option>
        </select>
      </label>
      <input type="search" class="search-input" placeholder="Search Pokémon…">
      <ul class="results-list"></ul>
      <label>Nature
        <select class="nature-select"></select>
      </label>
      <div class="actions">
        <button class="btn-show" disabled>Show</button>
        <button class="btn-clear">Clear</button>
      </div>
    </div>
  </div>
  <script src="/control.js"></script>
</body>
</html>
```

- [x] **Step 2: Create wwwroot/control.js**

```javascript
// control.js
const STAT_ABBREV = {
  'hp':'HP','attack':'Atk','defense':'Def',
  'special-attack':'SpA','special-defense':'SpD','speed':'Spe',
};

function natureLabel(n) {
  if (!n.increasedStat) return n.displayName;
  return `${n.displayName} (+${STAT_ABBREV[n.increasedStat]} / -${STAT_ABBREV[n.decreasedStat]})`;
}

async function loadNatures(select) {
  const res = await fetch('/api/natures');
  const natures = await res.json();
  select.innerHTML = natures
    .map(n => `<option value="${n.name}">${natureLabel(n)}</option>`)
    .join('');
  // Default to Hardy
  const hardy = [...select.options].find(o => o.value === 'hardy');
  if (hardy) hardy.selected = true;
}

function debounce(fn, ms) {
  let timer;
  return (...args) => { clearTimeout(timer); timer = setTimeout(() => fn(...args), ms); };
}

function initPanel(panel) {
  const slot      = panel.dataset.slot;
  const search    = panel.querySelector('.search-input');
  const results   = panel.querySelector('.results-list');
  const natureEl  = panel.querySelector('.nature-select');
  const variantEl = panel.querySelector('.variant-select');
  const btnShow   = panel.querySelector('.btn-show');
  const btnClear  = panel.querySelector('.btn-clear');
  let selectedPokemon = null;

  loadNatures(natureEl);

  const doSearch = debounce(async (q) => {
    if (!q.trim()) { results.innerHTML = ''; return; }
    const res = await fetch(`/api/pokemon/search?q=${encodeURIComponent(q)}&limit=8`);
    const list = await res.json();
    results.innerHTML = list.map(p =>
      `<li data-name="${p.name}">${p.displayName}</li>`
    ).join('');
    results.querySelectorAll('li').forEach(li => {
      li.addEventListener('click', () => {
        selectedPokemon = li.dataset.name;
        results.querySelectorAll('li').forEach(l => l.classList.remove('selected'));
        li.classList.add('selected');
        btnShow.disabled = false;
      });
    });
  }, 150);

  search.addEventListener('input', e => doSearch(e.target.value));

  btnShow.addEventListener('click', async () => {
    if (!selectedPokemon) return;
    await fetch('/api/overlay/set', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        slot:           slot,
        pokemonName:    selectedPokemon,
        natureName:     natureEl.value,
        spriteVariant:  variantEl.value,
      }),
    });
  });

  btnClear.addEventListener('click', async () => {
    await fetch('/api/overlay/clear', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ slot }),
    });
  });
}

// Initialise both panels
document.querySelectorAll('.panel').forEach(initPanel);

// Live indicators — connect to overlay WS to track slot state
function connectIndicator() {
  const ws = new WebSocket(`ws://${location.host}/ws/overlay`);
  const indLeft  = document.getElementById('ind-left');
  const indRight = document.getElementById('ind-right');

  function update(snapshot) {
    indLeft.className  = 'indicator' + (snapshot.left  ? ' active' : '');
    indRight.className = 'indicator' + (snapshot.right ? ' active' : '');
  }

  ws.onmessage = (e) => {
    const msg = JSON.parse(e.data);
    if (msg.type === 'snapshot') {
      update(msg.snapshot);
    } else if (msg.type === 'slotUpdate') {
      const snap = {
        left:  indLeft.classList.contains('active')  ? {} : null,
        right: indRight.classList.contains('active') ? {} : null,
      };
      snap[msg.slot] = msg.data;
      update(snap);
    } else if (msg.type === 'slotClear') {
      const snap = {
        left:  indLeft.classList.contains('active')  ? {} : null,
        right: indRight.classList.contains('active') ? {} : null,
      };
      snap[msg.slot] = null;
      update(snap);
    }
  };

  ws.onclose = () => setTimeout(connectIndicator, 1500);
}

connectIndicator();
```

- [x] **Step 3: Create wwwroot/control.css**

```css
/* control.css */
* { box-sizing: border-box; margin: 0; padding: 0; }

body {
  font-family: 'Segoe UI', system-ui, sans-serif;
  background: #1a1a2e;
  color: #e0e0e0;
  padding: 16px;
}

#app {
  display: flex;
  gap: 24px;
  max-width: 900px;
}

.panel {
  flex: 1;
  background: #16213e;
  border-radius: 10px;
  padding: 16px;
  display: flex;
  flex-direction: column;
  gap: 10px;
}

h2 {
  font-size: 16px;
  display: flex;
  align-items: center;
  gap: 8px;
}

.indicator {
  width: 10px;
  height: 10px;
  border-radius: 50%;
  background: #555;
  display: inline-block;
  transition: background 0.3s;
}
.indicator.active { background: #66bb6a; }

label {
  display: flex;
  flex-direction: column;
  gap: 4px;
  font-size: 12px;
  opacity: 0.7;
}

select, input[type="search"] {
  width: 100%;
  padding: 6px 8px;
  background: #0f3460;
  color: #e0e0e0;
  border: 1px solid #1a5276;
  border-radius: 6px;
  font-size: 13px;
}

.results-list {
  list-style: none;
  max-height: 180px;
  overflow-y: auto;
  border: 1px solid #1a5276;
  border-radius: 6px;
  background: #0f3460;
}

.results-list li {
  padding: 6px 10px;
  cursor: pointer;
  font-size: 13px;
}
.results-list li:hover    { background: #1a5276; }
.results-list li.selected { background: #1a5276; font-weight: bold; color: #82b1ff; }

.actions { display: flex; gap: 8px; }

button {
  flex: 1;
  padding: 8px;
  border: none;
  border-radius: 6px;
  font-size: 13px;
  font-weight: 600;
  cursor: pointer;
  transition: opacity 0.2s;
}

.btn-show              { background: #1565c0; color: #fff; }
.btn-show:disabled     { opacity: 0.4; cursor: default; }
.btn-show:not(:disabled):hover { background: #1976d2; }

.btn-clear             { background: #424242; color: #e0e0e0; }
.btn-clear:hover       { background: #616161; }
```

- [x] **Step 4: Start server and test the control UI**

```bash
cd src/PokemonOverlay && dotnet run
```

Open http://localhost:5000/control in a browser.

Test checklist:
- [x] Both panels load with nature dropdowns populated (Hardy selected by default)
- [x] Nature dropdown shows modifiers: e.g. "Jolly (+Spe / -SpA)"
- [x] Typing "char" in left search shows Charmander and Charizard in the results list
- [x] Clicking a result selects it (highlighted) and enables the Show button
- [x] Clicking Show posts to `/api/overlay/set` (check network tab: 200 response)
- [x] Live indicator turns green for the corresponding slot
- [x] Opening /overlay simultaneously shows the card appear with the Pokéball animation
- [x] Clicking Clear posts to `/api/overlay/clear`; indicator goes grey; overlay card empties

- [x] **Step 5: Test live indicator sync**

Open /overlay and /control side-by-side. Set and clear slots using the control panel and verify the overlay responds in real time.

- [x] **Step 6: Commit**

```bash
git add src/PokemonOverlay/wwwroot/control.html src/PokemonOverlay/wwwroot/control.js src/PokemonOverlay/wwwroot/control.css
git commit -m "feat: add control panel UI with live search, nature picker, and slot management"
```

---

## Self-Review

### Spec coverage

| Spec requirement | Task |
|---|---|
| GET /api/pokemon/search with scoring (exact=1000, prefix=500, substring=100) | Task 3 (DataService), Task 5 (DataEndpoints) |
| GET /api/pokemon/{name} | Task 5 (DataEndpoints) |
| GET /api/natures | Task 5 (DataEndpoints) |
| GET /api/items | Task 5 (DataEndpoints) |
| POST /api/overlay/set — sprite variant fallback | Task 5 (OverlayEndpoints) |
| POST /api/overlay/clear | Task 5 (OverlayEndpoints) |
| WS /ws/overlay — snapshot on connect, slotUpdate, slotClear | Task 4 (OverlayStateService), Task 5 (OverlayEndpoints) |
| Sprites served at /data/sprites/{spritePath} | Task 5 (Program.cs secondary static files) |
| OVERLAY_BIND_URL env var | Task 5 (Program.cs) |
| OVERLAY_DATA_PATH env var | Task 5 (Program.cs, DataService) |
| DataService is a singleton | Task 5 (Program.cs `AddSingleton`) |
| OverlayStateService lock for mutation, broadcast outside lock | Task 4 (OverlayStateService) |
| Overlay: two slots, VS between, sprite, name, type pills, nature row, stat table | Task 6 (overlay.js renderCard) |
| Nature-boosted row green, hindered row red | Task 6 (overlay.css + overlay.js) |
| Pokéball spinner ~500ms, fade 250ms | Task 6 (overlay.js animateSlot, overlay.css) |
| WebSocket auto-reconnect 1500ms | Task 6 (overlay.js connect/onclose) |
| Control: sprite variant dropdown | Task 7 (control.html) |
| Control: live search debounced 150ms | Task 7 (control.js debounce) |
| Control: nature dropdown with shorthand ("Jolly (+Spe / -SpA)") | Task 7 (control.js natureLabel) |
| Control: Show button disabled until Pokémon selected | Task 7 (control.js btnShow.disabled) |
| Control: live indicator green when slot active | Task 7 (control.js connectIndicator) |
| /overlay and /control URL routes (no .html) | Task 5 (Program.cs MapGet) |

All requirements covered. ✓
