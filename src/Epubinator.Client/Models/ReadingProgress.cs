namespace Epubinator.Client.Models;

public class ReadingProgress
{
    public string BookId { get; set; } = string.Empty;
    public int ChapterIndex { get; set; }
    public double ScrollPercent { get; set; }
    /// <summary>Unix milliseconds timestamp of the last time this book was opened for reading.</summary>
    public long LastReadAtMs { get; set; }
}
