namespace Epubinator.Client.Models;

public class ReaderSettings
{
    public string Theme { get; set; } = "light";
    public string FontFamily { get; set; } = "system-ui, -apple-system, sans-serif";
    public int FontSize { get; set; } = 16;
    /// <summary>Screen orientation lock: "none" (auto), "portrait", or "landscape".</summary>
    public string OrientationLock { get; set; } = "none";
}
