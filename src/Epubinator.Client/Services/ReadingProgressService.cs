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
            ScrollPercent = scrollPercent,
            LastReadAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        var json = JsonSerializer.Serialize(progress, _json);
        await _js.InvokeVoidAsync("localStorage.setItem", Key(bookId), json);
    }

    /// <summary>Stamps the current time as LastReadAtMs for a book without changing chapter or scroll.</summary>
    public async Task TouchAsync(string bookId)
    {
        var existing = await GetAsync(bookId);
        var progress = new ReadingProgress
        {
            BookId = bookId,
            ChapterIndex = existing?.ChapterIndex ?? 0,
            ScrollPercent = existing?.ScrollPercent ?? 0,
            LastReadAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
        var json = JsonSerializer.Serialize(progress, _json);
        await _js.InvokeVoidAsync("localStorage.setItem", Key(bookId), json);
    }

    /// <summary>Returns a mapping of bookId → LastReadAtMs for the given set of book IDs.</summary>
    public async Task<Dictionary<string, long>> GetAllLastReadAsync(IEnumerable<string> bookIds)
    {
        var ids = bookIds.ToList();
        var tasks = ids.Select(id => GetAsync(id));
        var results = await Task.WhenAll(tasks);
        return ids.Zip(results, (id, progress) => (id, progress))
                  .ToDictionary(x => x.id, x => x.progress?.LastReadAtMs ?? 0);
    }

    public async Task DeleteAsync(string bookId)
        => await _js.InvokeVoidAsync("localStorage.removeItem", Key(bookId));

    private static string Key(string bookId) => $"progress_{bookId}";
}
