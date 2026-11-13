namespace WidBar.DailyVerse.Core.Models;

public sealed record BibleBook(
    string Id,
    string Name,
    string ShortName,
    int Order,
    bool IsNewTestament);

public sealed record BibleVerse(
    string BookId,
    string BookName,
    string ShortBookName,
    int Chapter,
    int Verse,
    string Text)
{
    public string Key => $"{BookId}:{Chapter}:{Verse}";
    public string Reference => $"{BookName} {Chapter}:{Verse}";
    public string ShortReference => $"{ShortBookName} {Chapter}:{Verse}";
}
