using System.Text.Json;
using Microsoft.JSInterop;
using Epubinator.Client.Models;

namespace Epubinator.Client.Services;

/// <summary>
/// Manages reader appearance settings (theme, font, size) in localStorage and
/// applies them to the document via CSS custom properties through JS interop.
/// </summary>
public class ThemeService
{
    private readonly IJSRuntime _js;
    private const string StorageKey = "reader_settings";
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ReaderSettings Settings { get; private set; } = new();

    /// <summary>Raised when settings change so components can react.</summary>
    public event Action? OnSettingsChanged;

    public ThemeService(IJSRuntime js) => _js = js;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Loads persisted settings from localStorage and applies them to the DOM.</summary>
    public async Task InitialiseAsync()
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", StorageKey);
            if (!string.IsNullOrEmpty(raw))
                Settings = JsonSerializer.Deserialize<ReaderSettings>(raw, _json) ?? new();
        }
        catch { /* use defaults */ }

        await ApplyAsync();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public async Task UpdateAsync(ReaderSettings settings)
    {
        Settings = settings;
        var json = JsonSerializer.Serialize(settings, _json);
        await _js.InvokeVoidAsync("localStorage.setItem", StorageKey, json);
        await ApplyAsync();
        OnSettingsChanged?.Invoke();
    }

    /// <summary>Pushes current settings to the document as CSS custom properties.</summary>
    public async Task ApplyAsync()
    {
        await _js.InvokeVoidAsync(
            "epubInterop.applyTheme",
            Settings.Theme,
            Settings.FontFamily,
            Settings.FontSize);
    }
}
