using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using WidBar.DailyVerse.Core.Models;

namespace WidBar.DailyVerse.Core.Services;

public sealed class DailyVerseStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _filePath;

    public DailyVerseStateStore(string directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new ArgumentException("Diretório de dados não informado.", nameof(directory));
        }

        _filePath = Path.Combine(directory, "daily-state.json");
    }

    public (DailyVerseState State, string? Warning) Load()
    {
        if (!File.Exists(_filePath))
        {
            return (new DailyVerseState(), null);
        }

        try
        {
            var json = File.ReadAllText(_filePath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json))
            {
                return (new DailyVerseState(), null);
            }

            var state = JsonSerializer.Deserialize<DailyVerseState>(json, JsonOptions);
            if (state is null)
            {
                return (new DailyVerseState(), "Arquivo de estado vazio; novo versículo sorteado.");
            }

            state.RecentHistory ??= [];
            state.Favorites ??= [];
            return (state, null);
        }
        catch (Exception ex)
        {
            return (new DailyVerseState(), $"Não foi possível ler o histórico salvo ({ex.Message}).");
        }
    }

    public string? Save(DailyVerseState state)
    {
        try
        {
            var dir = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(dir);

            var tmp = _filePath + $".{Guid.NewGuid():N}.tmp";
            var json = JsonSerializer.Serialize(state, JsonOptions);
            File.WriteAllText(tmp, json, new UTF8Encoding(false));
            File.Move(tmp, _filePath, overwrite: true);
            return null;
        }
        catch (Exception ex)
        {
            return $"Erro ao salvar estado diário: {ex.Message}";
        }
    }
}
