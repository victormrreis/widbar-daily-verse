using WidBar.DailyVerse.Core.Data;
using WidBar.DailyVerse.Core.Models;
using WidBar.DailyVerse.Core.Services;
using Xunit;

namespace WidBar.DailyVerse.Tests;

public sealed class DailyVerseCoreTests : IDisposable
{
    private readonly string _tempDir = Path.Combine(Path.GetTempPath(), "WidBarDailyVerseTests_" + Guid.NewGuid().ToString("N"));
    private readonly BibleRepository _repo = new();

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Fact]
    public void EmbeddedBible_LoadsAll66BooksAndAllVerses()
    {
        Assert.Equal(66, _repo.Books.Count);
        Assert.Equal(31102, _repo.TotalVerseCount);

        Assert.True(_repo.TryGetVerse("JHN:3:16", out var john316));
        Assert.Equal("João 3:16", john316.Reference);
        Assert.Equal("Jo 3:16", john316.ShortReference);
        Assert.Contains("amou", john316.Text, StringComparison.OrdinalIgnoreCase);

        var psalm23 = _repo.GetChapter("PSA", 23);
        Assert.Equal(6, psalm23.Count);
    }

    [Fact]
    public void CuratedVerses_AllExistAndHaveNoDuplicates()
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var missing = new List<string>();
        var duplicates = new List<string>();

        foreach (var key in CuratedVerses.Keys)
        {
            if (!seen.Add(key))
            {
                duplicates.Add(key);
            }

            if (!_repo.TryGetVerse(key, out var verse) || string.IsNullOrWhiteSpace(verse.Text))
            {
                missing.Add(key);
            }
        }

        Assert.Empty(duplicates);
        Assert.Empty(missing);
        Assert.True(_repo.CuratedVerseCount >= 366, $"Esperava >= 366 versículos curados, obteve {_repo.CuratedVerseCount}");
    }

    [Fact]
    public void Coordinator_KeepsSameVerseSameDay_AndRotatesAtNextDay()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.FromHours(-3)));
        var store = new DailyVerseStateStore(_tempDir);

        string day1Key;
        using (var coord1 = new DailyVerseCoordinator(_repo, store, clock))
        {
            var snap1 = coord1.EnsureTodayVerse(VersePoolMode.Devotional);
            day1Key = snap1.Verse.Key;
            Assert.Equal(new DateOnly(2026, 11, 12), snap1.Date);
        }

        // Reinicia no mesmo dia -> deve manter exatamente o mesmo versículo
        clock.Advance(TimeSpan.FromHours(5));
        using (var coord2 = new DailyVerseCoordinator(_repo, store, clock))
        {
            var snap2 = coord2.EnsureTodayVerse(VersePoolMode.Devotional);
            Assert.Equal(day1Key, snap2.Verse.Key);

            // Avança para o dia seguinte após meia-noite -> deve sortear um novo versículo
            clock.Advance(TimeSpan.FromHours(10));
            var snap3 = coord2.EnsureTodayVerse(VersePoolMode.Devotional);
            Assert.Equal(new DateOnly(2026, 11, 13), snap3.Date);
            Assert.NotEqual(day1Key, snap3.Verse.Key);
        }
    }

    [Fact]
    public void Coordinator_ManualShuffleAndFavoritesWork()
    {
        var clock = new FakeTimeProvider(new DateTimeOffset(2026, 11, 12, 22, 0, 0, TimeSpan.FromHours(-3)));
        var store = new DailyVerseStateStore(_tempDir);
        using var coord = new DailyVerseCoordinator(_repo, store, clock);

        var initial = coord.EnsureTodayVerse(VersePoolMode.Devotional);
        var shuffled = coord.ShuffleNow(VersePoolMode.Devotional);
        Assert.NotEqual(initial.Verse.Key, shuffled.Verse.Key);
        Assert.True(shuffled.IsManualShuffle);

        var favSnap = coord.ToggleFavorite(shuffled.Verse.Key);
        Assert.True(favSnap.IsFavorite);
        Assert.Equal(1, favSnap.FavoriteCount);

        var favOnly = coord.ShuffleNow(VersePoolMode.FavoritesOnly);
        Assert.Equal(shuffled.Verse.Key, favOnly.Verse.Key);
        Assert.False(favOnly.FallbackFromEmptyFavorites);
    }

    [Fact]
    public void WidgetSettings_NormalizesAndComputesWidths()
    {
        var invalid = WidgetSettings.FromJson("{invalid json");
        Assert.Equal(PreviewDisplayMode.VerseAndReference, invalid.DisplayMode);
        Assert.Equal(300, invalid.ComputePreviewWidth());

        var iconOnly = new WidgetSettings { DisplayMode = PreviewDisplayMode.IconOnly, PreviewWidth = 999 };
        iconOnly.Normalize();
        Assert.Equal(460, iconOnly.PreviewWidth);
        Assert.Equal(42, iconOnly.ComputePreviewWidth());
    }

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly TimeSpan _offset;
        private DateTimeOffset _now;

        public FakeTimeProvider(DateTimeOffset start)
        {
            _offset = start.Offset;
            _now = start;
        }

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.CreateCustomTimeZone("BRT", _offset, "BRT", "BRT");
        public override DateTimeOffset GetUtcNow() => _now.ToUniversalTime();
        public void Advance(TimeSpan delta) => _now = _now.Add(delta);
    }
}
