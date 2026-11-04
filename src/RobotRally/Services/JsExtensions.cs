using Microsoft.JSInterop;

namespace RobotRally.Services;

/// <summary>Fire-and-forget helpers for rally.js (a closed circuit must never crash the game).</summary>
public static class JsExtensions
{
    public static async void Sfx(this IJSRuntime js, string name)
    {
        try { await js.InvokeVoidAsync("rally.sfx", name); } catch { }
    }

    public static async void Confetti(this IJSRuntime js, int count = 140, double x = 0.5, double y = 0.45)
    {
        try { await js.InvokeVoidAsync("rally.confetti", count, x, y); } catch { }
    }
}
