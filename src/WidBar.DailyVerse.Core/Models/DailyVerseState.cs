namespace WidBar.DailyVerse.Core.Models;

public sealed class DailyVerseState
{
    public string Date { get; set; } = "";
    public string ActiveVerseKey { get; set; } = "";
    public VersePoolMode ActivePool { get; set; } = VersePoolMode.Devotional;
    public List<string> RecentHistory { get; set; } = [];
    public List<string> Favorites { get; set; } = [];
    public DateTimeOffset LastUpdatedUtc { get; set; }
    public bool IsManualShuffle { get; set; }
}

public sealed record DailyVerseSnapshot(
    BibleVerse Verse,
    DateOnly Date,
    VersePoolMode EffectivePool,
    bool IsFavorite,
    int FavoriteCount,
    bool IsManualShuffle,
    bool FallbackFromEmptyFavorites,
    string? WarningMessage);
