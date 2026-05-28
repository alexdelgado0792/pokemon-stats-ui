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
            // Send current state immediately on connect so OBS restores after source reload
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
