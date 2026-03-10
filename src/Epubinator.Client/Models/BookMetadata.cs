namespace Epubinator.Client.Models;

public class BookMetadata
{
    public string Id { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Author { get; set; } = string.Empty;
    public string? CoverBase64 { get; set; }
    public int ChapterCount { get; set; }
    public long FileSizeBytes { get; set; }
    public long AddedAtMs { get; set; }

    public DateTime AddedAt => DateTimeOffset.FromUnixTimeMilliseconds(AddedAtMs).LocalDateTime;

    public string FileSizeDisplay => FileSizeBytes switch
    {
        < 1024 => $"{FileSizeBytes} B",
        < 1024 * 1024 => $"{FileSizeBytes / 1024.0:F1} KB",
        _ => $"{FileSizeBytes / (1024.0 * 1024.0):F1} MB"
    };
}
