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

// Primary static files: wwwroot/ → JS, CSS, HTML assets
app.UseStaticFiles();

// Secondary static files: OVERLAY_DATA_PATH → /data (sprites + JSON)
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(Path.GetFullPath(dataPath)),
    RequestPath  = "/data",
});

// Named routes for the two UIs (clean URLs without .html extension per spec)
app.MapGet("/overlay", (IWebHostEnvironment env) =>
    Results.File(Path.Combine(env.WebRootPath, "overlay.html"), "text/html"));

app.MapGet("/control", (IWebHostEnvironment env) =>
    Results.File(Path.Combine(env.WebRootPath, "control.html"), "text/html"));

app.MapDataEndpoints();
app.MapOverlayEndpoints();

app.Run();
