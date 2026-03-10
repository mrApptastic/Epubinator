namespace Epubinator.Client.Models;

public class ReadingProgress
{
    public string BookId { get; set; } = string.Empty;
    public int ChapterIndex { get; set; }
    public double ScrollPercent { get; set; }
}
