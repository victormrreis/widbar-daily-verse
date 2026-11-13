using System.Globalization;
using System.Security.Cryptography;
using WidBar.DailyVerse.Core.Models;

namespace WidBar.DailyVerse.Core.Services;

/// <summary>
/// Coordena a seleção diária aleatória, histórico anti-repetição e favoritos,
/// compartilhado em processo entre todas as instâncias (taskbar e Widget Board).
/// </summary>
public sealed class DailyVerseCoordinator : IDisposable
{
    private const int MaxHistoryCount = 365;

    private readonly BibleRepository _repository;
    private readonly DailyVerseStateStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly object _gate = new();

    private DailyVerseState _state;
    private VersePoolMode _preferredPool = VersePoolMode.Devotional;
    private string? _lastWarning;
    private ITimer? _rolloverTimer;
    private bool _disposed;

    public event Action<DailyVerseSnapshot>? StateChanged;

    public DailyVerseCoordinator(
        BibleRepository repository,
        DailyVerseStateStore store,
        TimeProvider? timeProvider = null)
    {
        _repository = repository;
        _store = store;
        _timeProvider = timeProvider ?? TimeProvider.System;
        (_state, _lastWarning) = _store.Load();
    }

    public DailyVerseSnapshot Current
    {
        get
        {
            lock (_gate)
            {
                return BuildSnapshotLocked(fallbackFromEmptyFavorites: false);
            }
        }
    }

    public void StartPeriodicRolloverCheck()
    {
        lock (_gate)
        {
            if (_disposed || _rolloverTimer is not null)
            {
                return;
            }

            // Verifica a cada 60s para trocar à meia-noite e logo após o Windows sair da suspensão.
            _rolloverTimer = _timeProvider.CreateTimer(
                _ => EnsureTodayVerse(_preferredPool, forcePoolAlignment: false),
                null,
                TimeSpan.FromSeconds(60),
                TimeSpan.FromSeconds(60));
        }
    }

    public DailyVerseSnapshot EnsureTodayVerse(VersePoolMode poolMode, bool forcePoolAlignment = false)
    {
        DailyVerseSnapshot snapshot;
        bool changed = false;

        lock (_gate)
        {
            _preferredPool = poolMode;
            var today = GetTodayDate();
            var todayStr = FormatDate(today);

            var hasValidVerse = _repository.TryGetVerse(_state.ActiveVerseKey, out _);
            var isNewDay = !string.Equals(_state.Date, todayStr, StringComparison.Ordinal);
            var poolChanged = forcePoolAlignment && _state.ActivePool != poolMode;

            if (isNewDay || !hasValidVerse || poolChanged)
            {
                snapshot = PickNextVerseLocked(today, poolMode, isManualShuffle: false);
                changed = true;
            }
            else
            {
                snapshot = BuildSnapshotLocked(fallbackFromEmptyFavorites: false);
            }
        }

        if (changed)
        {
            StateChanged?.Invoke(snapshot);
        }

        return snapshot;
    }

    public DailyVerseSnapshot ShuffleNow(VersePoolMode poolMode)
    {
        DailyVerseSnapshot snapshot;
        lock (_gate)
        {
            _preferredPool = poolMode;
            snapshot = PickNextVerseLocked(GetTodayDate(), poolMode, isManualShuffle: true);
        }

        StateChanged?.Invoke(snapshot);
        return snapshot;
    }

    public DailyVerseSnapshot SelectVerse(string verseKey)
    {
        if (!_repository.TryGetVerse(verseKey, out _))
        {
            return Current;
        }

        DailyVerseSnapshot snapshot;
        lock (_gate)
        {
            var today = GetTodayDate();
            _state.Date = FormatDate(today);
            _state.ActiveVerseKey = verseKey;
            _state.IsManualShuffle = true;
            _state.LastUpdatedUtc = _timeProvider.GetUtcNow();
            RecordHistoryLocked(verseKey);
            _lastWarning = _store.Save(_state);
            snapshot = BuildSnapshotLocked(fallbackFromEmptyFavorites: false);
        }

        StateChanged?.Invoke(snapshot);
        return snapshot;
    }

    public DailyVerseSnapshot ToggleFavorite(string verseKey)
    {
        if (!_repository.TryGetVerse(verseKey, out _))
        {
            return Current;
        }

        DailyVerseSnapshot snapshot;
        lock (_gate)
        {
            var idx = _state.Favorites.FindIndex(k => string.Equals(k, verseKey, StringComparison.Ordinal));
            if (idx >= 0)
            {
                _state.Favorites.RemoveAt(idx);
            }
            else
            {
                _state.Favorites.Add(verseKey);
            }

            _state.LastUpdatedUtc = _timeProvider.GetUtcNow();
            _lastWarning = _store.Save(_state);
            snapshot = BuildSnapshotLocked(fallbackFromEmptyFavorites: false);
        }

        StateChanged?.Invoke(snapshot);
        return snapshot;
    }

    public IReadOnlyList<BibleVerse> GetFavorites()
    {
        lock (_gate)
        {
            var result = new List<BibleVerse>(_state.Favorites.Count);
            foreach (var key in _state.Favorites)
            {
                if (_repository.TryGetVerse(key, out var verse))
                {
                    result.Add(verse);
                }
            }
            return result;
        }
    }

    public IReadOnlyList<BibleVerse> GetChapter(string bookId, int chapter) =>
        _repository.GetChapter(bookId, chapter);

    private DailyVerseSnapshot PickNextVerseLocked(DateOnly today, VersePoolMode requestedPool, bool isManualShuffle)
    {
        var (poolKeys, fallbackFromEmptyFavs) = _repository.ResolvePoolKeys(requestedPool, _state.Favorites);
        var effectivePool = fallbackFromEmptyFavs ? VersePoolMode.Devotional : requestedPool;

        var recentSet = new HashSet<string>(_state.RecentHistory, StringComparer.Ordinal);
        var candidates = poolKeys.Where(k => !recentSet.Contains(k)).ToList();

        // Se já percorreu todo o pool, evita apenas repetir imediatamente o versículo atual.
        if (candidates.Count == 0)
        {
            var currentKey = _state.ActiveVerseKey;
            candidates = poolKeys.Where(k => !string.Equals(k, currentKey, StringComparison.Ordinal)).ToList();
            if (candidates.Count == 0)
            {
                candidates = [.. poolKeys];
            }
        }

        var chosenKey = candidates[RandomNumberGenerator.GetInt32(candidates.Count)];

        _state.Date = FormatDate(today);
        _state.ActiveVerseKey = chosenKey;
        _state.ActivePool = effectivePool;
        _state.IsManualShuffle = isManualShuffle;
        _state.LastUpdatedUtc = _timeProvider.GetUtcNow();
        RecordHistoryLocked(chosenKey);
        _lastWarning = _store.Save(_state);

        return BuildSnapshotLocked(fallbackFromEmptyFavs);
    }

    private void RecordHistoryLocked(string key)
    {
        _state.RecentHistory.RemoveAll(k => string.Equals(k, key, StringComparison.Ordinal));
        _state.RecentHistory.Add(key);
        if (_state.RecentHistory.Count > MaxHistoryCount)
        {
            _state.RecentHistory.RemoveRange(0, _state.RecentHistory.Count - MaxHistoryCount);
        }
    }

    private DailyVerseSnapshot BuildSnapshotLocked(bool fallbackFromEmptyFavorites)
    {
        var verse = _repository.GetVerseOrDefault(_state.ActiveVerseKey);
        var date = DateOnly.TryParseExact(_state.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : GetTodayDate();
        var isFav = _state.Favorites.Contains(verse.Key, StringComparer.Ordinal);

        return new DailyVerseSnapshot(
            Verse: verse,
            Date: date,
            EffectivePool: _state.ActivePool,
            IsFavorite: isFav,
            FavoriteCount: _state.Favorites.Count,
            IsManualShuffle: _state.IsManualShuffle,
            FallbackFromEmptyFavorites: fallbackFromEmptyFavorites,
            WarningMessage: _lastWarning);
    }

    private DateOnly GetTodayDate() =>
        DateOnly.FromDateTime(_timeProvider.GetLocalNow().DateTime);

    private static string FormatDate(DateOnly date) =>
        date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            _rolloverTimer?.Dispose();
            _rolloverTimer = null;
        }
    }
}
