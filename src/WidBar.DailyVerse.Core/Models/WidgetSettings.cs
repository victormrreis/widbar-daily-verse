using System.Text.Json;
using System.Text.Json.Serialization;

namespace WidBar.DailyVerse.Core.Models;

public sealed class WidgetSettings
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    public PreviewDisplayMode DisplayMode { get; set; } = PreviewDisplayMode.VerseAndReference;
    public VersePoolMode PoolMode { get; set; } = VersePoolMode.Devotional;
    public int PreviewWidth { get; set; } = 300;
    public bool ShowVerseIcon { get; set; } = true;
    public bool SubtleWarmAccent { get; set; } = true;

    public void Normalize()
    {
        if (!Enum.IsDefined(DisplayMode))
        {
            DisplayMode = PreviewDisplayMode.VerseAndReference;
        }

        if (!Enum.IsDefined(PoolMode))
        {
            PoolMode = VersePoolMode.Devotional;
        }

        PreviewWidth = Math.Clamp(PreviewWidth, 180, 460);
    }

    public int ComputePreviewWidth() => DisplayMode switch
    {
        PreviewDisplayMode.IconOnly => 42,
        PreviewDisplayMode.ReferenceOnly => ShowVerseIcon ? 148 : 122,
        _ => Math.Clamp(PreviewWidth, 180, 460),
    };

    public static WidgetSettings FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new WidgetSettings();
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<WidgetSettings>(json, JsonOptions) ?? new WidgetSettings();
            parsed.Normalize();
            return parsed;
        }
        catch (JsonException)
        {
            return new WidgetSettings();
        }
    }

    public string ToJson()
    {
        Normalize();
        return JsonSerializer.Serialize(this, JsonOptions);
    }
}
