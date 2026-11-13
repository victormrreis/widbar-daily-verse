using WidBar.DailyVerse.Core.Services;

namespace WidBar.DailyVerse.ExtensionApp;

/// <summary>
/// Mantém o repositório bíblico e o coordenador diário compartilhados no processo
/// para que todas as instâncias (monitores e Widget Board) exibam o mesmo versículo do dia.
/// </summary>
internal static class SharedVerseService
{
    private static readonly object Gate = new();
    private static BibleRepository? _repository;
    private static DailyVerseCoordinator? _coordinator;

    public static (BibleRepository Repository, DailyVerseCoordinator Coordinator) GetOrCreate(string sharedDataDirectory)
    {
        lock (Gate)
        {
            _repository ??= new BibleRepository();
            if (_coordinator is null)
            {
                var store = new DailyVerseStateStore(sharedDataDirectory);
                _coordinator = new DailyVerseCoordinator(_repository, store);
                _coordinator.StartPeriodicRolloverCheck();
            }

            return (_repository, _coordinator);
        }
    }
}
