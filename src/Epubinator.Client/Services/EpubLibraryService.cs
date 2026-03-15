using System.Text.Json;
using Microsoft.JSInterop;
using Epubinator.Client.Models;

namespace Epubinator.Client.Services;

/// <summary>
/// Manages the epub library stored in IndexedDB via Dexie.js JS interop.
/// Maintains an in-memory list of book metadata for fast UI access.
/// </summary>
public class EpubLibraryService
{
    private readonly IJSRuntime _js;
    private readonly EpubReaderService _readerService;

    public List<BookMetadata> Books { get; private set; } = [];

    /// <summary>Raised whenever Books changes (add / delete / reload).</summary>
    public event Action? OnBooksChanged;

    public EpubLibraryService(IJSRuntime js, EpubReaderService readerService)
    {
        _js = js;
        _readerService = readerService;
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    /// <summary>Loads persisted book metadata from IndexedDB on first launch.</summary>
    public async Task InitialiseAsync()
    {
        try
        {
            var books = await _js.InvokeAsync<List<BookMetadata>>("epubInterop.getAllBookMetadata");
            Books = books ?? [];
            Books.Sort((a, b) => b.AddedAtMs.CompareTo(a.AddedAtMs));
            OnBooksChanged?.Invoke();
        }
        catch
        {
            Books = [];
        }
    }

    // ── CRUD ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads an epub file from an IBrowserFile, extracts metadata, stores bytes in
    /// IndexedDB and updates the in-memory list.
    /// </summary>
    public async Task<BookMetadata?> AddBookAsync(Microsoft.AspNetCore.Components.Forms.IBrowserFile file)
    {
        // Read the file (up to 100 MB)
        const long maxSize = 100L * 1024 * 1024;
        using var stream = file.OpenReadStream(maxAllowedSize: maxSize);
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms);
        var bytes = ms.ToArray();

        var id = Guid.NewGuid().ToString();

        // Extract metadata via EpubReaderService (does not change loaded-book state)
        var meta = await _readerService.ExtractMetadataAsync(bytes, id);

        // Serialise with camelCase so Dexie stores sensible JS object keys
        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        var metaJson = JsonSerializer.Serialize(meta, options);

        // Persist to IndexedDB
        await _js.InvokeVoidAsync("epubInterop.addBook", id, metaJson, bytes);

        // Update in-memory list
        Books.Insert(0, meta);
        OnBooksChanged?.Invoke();
        return meta;
    }

    /// <summary>
    /// Re-sorts Books so that the most recently read books appear first.
    /// Books that have never been read are sorted by AddedAtMs (most recently added first).
    /// Fires <see cref="OnBooksChanged"/> after reordering.
    /// </summary>
    public void SortByLastRead(IReadOnlyDictionary<string, long> lastReadTimes)
    {
        Books.Sort((a, b) =>
        {
            var aTime = lastReadTimes.TryGetValue(a.Id, out var at) ? at : 0;
            var bTime = lastReadTimes.TryGetValue(b.Id, out var bt) ? bt : 0;
            if (bTime != aTime) return bTime.CompareTo(aTime);
            return b.AddedAtMs.CompareTo(a.AddedAtMs);
        });
        OnBooksChanged?.Invoke();
    }

    /// <summary>
    /// Moves the specified book to the front of the <see cref="Books"/> list and fires <see cref="OnBooksChanged"/>.
    /// Call this whenever a book is opened so the library list stays ordered by last-read.
    /// </summary>
    public void MoveToFront(string bookId)
    {
        var book = Books.FirstOrDefault(b => b.Id == bookId);
        if (book is null) return;
        Books.Remove(book);
        Books.Insert(0, book);
        OnBooksChanged?.Invoke();
    }

    /// <summary>Returns the raw epub bytes for the given book id from IndexedDB.</summary>
    public async Task<byte[]?> GetBookBytesAsync(string bookId)
    {
        try
        {
            return await _js.InvokeAsync<byte[]?>("epubInterop.getBookBytes", bookId);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Removes a book from IndexedDB and the in-memory list.</summary>
    public async Task DeleteBookAsync(string bookId)
    {
        await _js.InvokeVoidAsync("epubInterop.deleteBook", bookId);
        Books.RemoveAll(b => b.Id == bookId);
        OnBooksChanged?.Invoke();
    }
}
