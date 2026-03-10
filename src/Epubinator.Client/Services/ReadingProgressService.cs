using System.Text.Json;
using Microsoft.JSInterop;
using Epubinator.Client.Models;

namespace Epubinator.Client.Services;

/// <summary>
/// Persists reading progress (chapter index + scroll position) in localStorage via JS interop.
/// Key format: "progress_{bookId}"
/// </summary>
public class ReadingProgressService
{
    private readonly IJSRuntime _js;
    private static readonly JsonSerializerOptions _json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public ReadingProgressService(IJSRuntime js) => _js = js;

    public async Task<ReadingProgress?> GetAsync(string bookId)
    {
        try
        {
            var raw = await _js.InvokeAsync<string?>("localStorage.getItem", Key(bookId));
            if (string.IsNullOrEmpty(raw)) return null;
            return JsonSerializer.Deserialize<ReadingProgress>(raw, _json);
        }
        catch { return null; }
    }

    public async Task SaveAsync(string bookId, int chapterIndex, double scrollPercent)
    {
        var progress = new ReadingProgress
        {
            BookId = bookId,
            ChapterIndex = chapterIndex,
            ScrollPercent = scrollPercent
        };
        var json = JsonSerializer.Serialize(progress, _json);
        await _js.InvokeVoidAsync("localStorage.setItem", Key(bookId), json);
    }

    public async Task DeleteAsync(string bookId)
        => await _js.InvokeVoidAsync("localStorage.removeItem", Key(bookId));

    private static string Key(string bookId) => $"progress_{bookId}";
}
