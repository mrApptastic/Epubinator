using AngleSharp;
using AngleSharp.Dom;
using VersOne.Epub;
using Epubinator.Client.Models;

namespace Epubinator.Client.Services;

/// <summary>
/// Parses epub files using VersOne.Epub, processes chapter HTML (sanitises scripts,
/// patches image src attributes with inline data URIs), and extracts book metadata.
/// Stateful: the last loaded book is held in memory.
/// </summary>
public class EpubReaderService
{
    private EpubBook? _currentBook;
    private Dictionary<string, string> _imageDict = new(StringComparer.OrdinalIgnoreCase);

    public int ChapterCount => _currentBook?.ReadingOrder.Count ?? 0;

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Extracts lightweight metadata from epub bytes without retaining state.</summary>
    public async Task<BookMetadata> ExtractMetadataAsync(byte[] bytes, string bookId)
    {
        using var ms = new MemoryStream(bytes);
        var book = await EpubReader.ReadBookAsync(ms);

        return new BookMetadata
        {
            Id = bookId,
            Title = book.Title ?? "Unknown Title",
            Author = BuildAuthors(book),
            CoverBase64 = BuildCoverBase64(book),
            ChapterCount = book.ReadingOrder.Count,
            FileSizeBytes = bytes.Length,
            AddedAtMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()
        };
    }

    /// <summary>Fully loads an epub into memory in preparation for chapter rendering.</summary>
    public async Task LoadBookAsync(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        _currentBook = await EpubReader.ReadBookAsync(ms);
        _imageDict = BuildImageDict(_currentBook);
    }

    /// <summary>Returns sanitised, image-patched HTML for the given chapter index.</summary>
    public async Task<string> GetChapterHtmlAsync(int index)
    {
        if (_currentBook is null)
            return "<p>No book loaded.</p>";

        var readingOrder = _currentBook.ReadingOrder;
        if (index < 0 || index >= readingOrder.Count)
            return "<p>Chapter not found.</p>";

        var rawHtml = readingOrder[index].Content ?? string.Empty;
        return await ProcessChapterHtmlAsync(rawHtml);
    }

    // ── HTML processing ───────────────────────────────────────────────────────

    private async Task<string> ProcessChapterHtmlAsync(string rawHtml)
    {
        var config = Configuration.Default;
        var context = BrowsingContext.New(config);
        var document = await context.OpenAsync(req => req.Content(rawHtml));

        // Strip script tags (security)
        foreach (var script in document.QuerySelectorAll("script").ToList())
            script.Remove();

        // Patch image srcs with inline data URIs
        foreach (var img in document.QuerySelectorAll("img"))
        {
            var src = img.GetAttribute("src");
            if (string.IsNullOrEmpty(src)) continue;
            var dataUri = ResolveImageSrc(src);
            if (dataUri is not null)
                img.SetAttribute("src", dataUri);
            else
                img.SetAttribute("alt", img.GetAttribute("alt") ?? "[image]");
        }

        // Preserve any <style> blocks from the epub chapter head
        var headStyles = string.Concat(
            document.Head?.QuerySelectorAll("style").Select(s => s.OuterHtml) ?? []);

        return headStyles + (document.Body?.InnerHtml ?? rawHtml);
    }

    private string? ResolveImageSrc(string src)
    {
        if (_imageDict.TryGetValue(src, out var uri)) return uri;

        var decoded = Uri.UnescapeDataString(src);
        if (_imageDict.TryGetValue(decoded, out uri)) return uri;

        var filename = Path.GetFileName(src);
        if (_imageDict.TryGetValue(filename, out uri)) return uri;

        var decodedFilename = Uri.UnescapeDataString(filename);
        if (_imageDict.TryGetValue(decodedFilename, out uri)) return uri;

        return null;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static Dictionary<string, string> BuildImageDict(EpubBook book)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var image in book.Content.Images.Local)
        {
            var key = image.Key;
            if (image.Content is null || image.Content.Length == 0) continue;
            var mime = GetMimeType(image.FilePath ?? key);
            var dataUri = $"data:{mime};base64,{Convert.ToBase64String(image.Content)}";
            dict[key] = dataUri;
            var filename = Path.GetFileName(key);
            dict.TryAdd(filename, dataUri);
        }

        return dict;
    }

    private static string BuildCoverBase64(EpubBook book)
    {
        var cover = book.CoverImage;
        if (cover is null || cover.Length == 0)
            return string.Empty;
        return $"data:image/jpeg;base64,{Convert.ToBase64String(cover)}";
    }

    private static string BuildAuthors(EpubBook book)
        => book.AuthorList.Count > 0 ? string.Join(", ", book.AuthorList) : "Unknown Author";

    private static string GetMimeType(string filePath) =>
        Path.GetExtension(filePath).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png"            => "image/png",
            ".gif"            => "image/gif",
            ".svg"            => "image/svg+xml",
            ".webp"           => "image/webp",
            _                 => "image/png"
        };
}
