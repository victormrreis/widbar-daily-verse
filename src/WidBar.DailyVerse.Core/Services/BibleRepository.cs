using System.IO.Compression;
using System.Reflection;
using WidBar.DailyVerse.Core.Data;
using WidBar.DailyVerse.Core.Models;

namespace WidBar.DailyVerse.Core.Services;

public sealed class BibleRepository
{
    private sealed class LoadedBible
    {
        public required List<BibleBook> Books { get; init; }
        public required Dictionary<string, BibleBook> BooksById { get; init; }
        public required List<BibleVerse> AllVerses { get; init; }
        public required Dictionary<string, BibleVerse> VersesByKey { get; init; }
        public required Dictionary<(string BookId, int Chapter), List<BibleVerse>> VersesByChapter { get; init; }
        public required List<string> AllKeys { get; init; }
        public required List<string> PsalmsProverbsNtKeys { get; init; }
        public required List<string> CuratedKeys { get; init; }
    }

    private static readonly IReadOnlyDictionary<string, string> ShortBookNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["GEN"] = "Gn", ["EXO"] = "Êx", ["LEV"] = "Lv", ["NUM"] = "Nm", ["DEU"] = "Dt",
        ["JOS"] = "Js", ["JDG"] = "Jz", ["RUT"] = "Rt", ["1SA"] = "1Sm", ["2SA"] = "2Sm",
        ["1KI"] = "1Rs", ["2KI"] = "2Rs", ["1CH"] = "1Cr", ["2CH"] = "2Cr", ["EZR"] = "Ed",
        ["NEH"] = "Ne", ["EST"] = "Et", ["JOB"] = "Jó", ["PSA"] = "Sl", ["PRO"] = "Pv",
        ["ECC"] = "Ec", ["SNG"] = "Ct", ["ISA"] = "Is", ["JER"] = "Jr", ["LAM"] = "Lm",
        ["EZK"] = "Ez", ["DAN"] = "Dn", ["HOS"] = "Os", ["JOL"] = "Jl", ["AMO"] = "Am",
        ["OBA"] = "Ob", ["JON"] = "Jn", ["MIC"] = "Mq", ["NAM"] = "Na", ["HAB"] = "Hc",
        ["ZEP"] = "Sf", ["HAG"] = "Ag", ["ZEC"] = "Zc", ["MAL"] = "Ml",
        ["MAT"] = "Mt", ["MRK"] = "Mc", ["LUK"] = "Lc", ["JHN"] = "Jo", ["ACT"] = "At",
        ["ROM"] = "Rm", ["1CO"] = "1Co", ["2CO"] = "2Co", ["GAL"] = "Gl", ["EPH"] = "Ef",
        ["PHP"] = "Fp", ["COL"] = "Cl", ["1TH"] = "1Ts", ["2TH"] = "2Ts", ["1TI"] = "1Tm",
        ["2TI"] = "2Tm", ["TIT"] = "Tt", ["PHM"] = "Fm", ["HEB"] = "Hb", ["JAS"] = "Tg",
        ["1PE"] = "1Pe", ["2PE"] = "2Pe", ["1JN"] = "1Jo", ["2JN"] = "2Jo", ["3JN"] = "3Jo",
        ["JUD"] = "Jd", ["REV"] = "Ap",
    };

    private readonly Lazy<LoadedBible> _data = new(LoadEmbeddedBible, LazyThreadSafetyMode.ExecutionAndPublication);

    public IReadOnlyList<BibleBook> Books => _data.Value.Books;
    public int TotalVerseCount => _data.Value.AllVerses.Count;
    public int CuratedVerseCount => _data.Value.CuratedKeys.Count;

    public bool TryGetVerse(string? key, out BibleVerse verse)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            verse = default!;
            return false;
        }

        return _data.Value.VersesByKey.TryGetValue(key.Trim(), out verse!);
    }

    public BibleVerse GetVerseOrDefault(string? key) =>
        TryGetVerse(key, out var verse)
            ? verse
            : _data.Value.VersesByKey["PSA:23:1"];

    public IReadOnlyList<BibleVerse> GetChapter(string bookId, int chapter) =>
        _data.Value.VersesByChapter.TryGetValue((bookId, chapter), out var list)
            ? list
            : [];

    public int GetChapterCount(string bookId)
    {
        var prefix = bookId;
        return _data.Value.VersesByChapter.Keys.Count(k => k.BookId == prefix);
    }

    public (IReadOnlyList<string> Keys, bool FallbackFromEmptyFavorites) ResolvePoolKeys(
        VersePoolMode mode,
        IReadOnlyList<string> favorites)
    {
        var data = _data.Value;
        switch (mode)
        {
            case VersePoolMode.FavoritesOnly:
                var validFavs = favorites.Where(data.VersesByKey.ContainsKey).Distinct(StringComparer.Ordinal).ToList();
                return validFavs.Count > 0
                    ? (validFavs, false)
                    : (data.CuratedKeys, true);

            case VersePoolMode.PsalmsProverbsNewTestament:
                return (data.PsalmsProverbsNtKeys, false);

            case VersePoolMode.FullBible:
                return (data.AllKeys, false);

            default:
                return (data.CuratedKeys, false);
        }
    }

    private static LoadedBible LoadEmbeddedBible()
    {
        var assembly = Assembly.GetExecutingAssembly();
        const string resourceName = "WidBar.DailyVerse.Core.Data.biblia-livre.tsv.gz";
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Recurso bíblico embarcado não encontrado: {resourceName}");
        using var gzip = new GZipStream(stream, CompressionMode.Decompress);
        using var reader = new StreamReader(gzip, System.Text.Encoding.UTF8);

        var books = new List<BibleBook>(66);
        var booksById = new Dictionary<string, BibleBook>(66, StringComparer.Ordinal);
        var allVerses = new List<BibleVerse>(32000);
        var versesByKey = new Dictionary<string, BibleVerse>(32000, StringComparer.Ordinal);
        var versesByChapter = new Dictionary<(string BookId, int Chapter), List<BibleVerse>>(1200);
        var allKeys = new List<string>(32000);
        var psalmsProverbsNtKeys = new List<string>(12000);

        var bookOrder = 0;
        var inNewTestament = false;
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith("B\t", StringComparison.Ordinal))
            {
                var parts = line.Split('\t');
                if (parts.Length < 3) continue;
                var id = parts[1];
                var name = parts[2];
                if (id == "MAT") inNewTestament = true;
                var shortName = ShortBookNames.GetValueOrDefault(id, id);
                var book = new BibleBook(id, name, shortName, ++bookOrder, inNewTestament);
                books.Add(book);
                booksById[id] = book;
            }
            else if (line.StartsWith("V\t", StringComparison.Ordinal))
            {
                var parts = line.Split('\t', 5);
                if (parts.Length < 5) continue;
                var bookId = parts[1];
                if (!booksById.TryGetValue(bookId, out var book)) continue;
                if (!int.TryParse(parts[2], out var chapter)) continue;
                if (!int.TryParse(parts[3], out var verseNum)) continue;
                var text = parts[4];

                var verse = new BibleVerse(book.Id, book.Name, book.ShortName, chapter, verseNum, text);
                allVerses.Add(verse);
                versesByKey[verse.Key] = verse;
                allKeys.Add(verse.Key);

                if (book.IsNewTestament || book.Id is "PSA" or "PRO")
                {
                    psalmsProverbsNtKeys.Add(verse.Key);
                }

                var chapterKey = (book.Id, chapter);
                if (!versesByChapter.TryGetValue(chapterKey, out var chapterList))
                {
                    chapterList = [];
                    versesByChapter[chapterKey] = chapterList;
                }
                chapterList.Add(verse);
            }
        }

        var curatedKeys = CuratedVerses.Keys
            .Where(versesByKey.ContainsKey)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new LoadedBible
        {
            Books = books,
            BooksById = booksById,
            AllVerses = allVerses,
            VersesByKey = versesByKey,
            VersesByChapter = versesByChapter,
            AllKeys = allKeys,
            PsalmsProverbsNtKeys = psalmsProverbsNtKeys,
            CuratedKeys = curatedKeys,
        };
    }
}
