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

    public async Task HandleWebSocketAsync(WebSocket ws, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        try
        {
            // Send current state immediately on connect so OBS restores after source reload
            await SendAsync(ws, new { type = "snapshot", snapshot = GetSnapshot() });
            _clients[id] = ws;

            // Read loop — overlay clients never send data; we just detect disconnect
            var buffer = new byte[128];
            var result = await ws.ReceiveAsync(buffer, cancellationToken);
            while (result.MessageType != WebSocketMessageType.Close
                   && ws.State == WebSocketState.Open)
            {
                result = await ws.ReceiveAsync(buffer, cancellationToken);
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
        var bytes  = Encode(message);
        var segment = new ArraySegment<byte>(bytes);
        var tasks  = new List<Task>();

        foreach (var (id, client) in _clients)
        {
            if (client.State != WebSocketState.Open)
            {
                _clients.TryRemove(id, out _);
                continue;
            }
            tasks.Add(SendToClientAsync(id, client, segment));
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);
    }

    private async Task SendToClientAsync(Guid id, WebSocket client, ArraySegment<byte> segment)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        try
        {
            await client.SendAsync(segment, WebSocketMessageType.Text, true, cts.Token);
        }
        catch
        {
            _clients.TryRemove(id, out _);
        }
    }

    private static Task SendAsync(WebSocket ws, object message) =>
        ws.SendAsync(new ArraySegment<byte>(Encode(message)),
            WebSocketMessageType.Text, true, CancellationToken.None);

    private static byte[] Encode(object message) =>
        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, JsonOpts));
}
