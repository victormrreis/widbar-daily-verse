using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WidBar.DailyVerse.Core.Models;
using Windows.UI;

namespace WidBar.DailyVerse.ExtensionApp.UI;

/// <summary>
/// Prévia exibida diretamente na barra de tarefas do Windows 11 pelo WidBar.
/// Fundo raiz nulo para preservar transparência/blur (incluindo Windhawk) e adaptação a taskbar normal ou compacta.
/// </summary>
public sealed class TaskbarPreviewControl : Grid
{
    private readonly BibleIconView _icon;
    private readonly StackPanel _twoLinePanel;
    private readonly StackPanel _refHeaderRow;
    private readonly TextBlock _refText;
    private readonly TextBlock _favoriteStar;
    private readonly TextBlock _verseLine;
    private readonly TextBlock _singleLineText;

    private WidgetSettings _settings = new();
    private DailyVerseSnapshot? _snapshot;
    private bool _isCompactTaskbar;

    public TaskbarPreviewControl()
    {
        Background = null;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Stretch;
        Padding = new Thickness(8, 0, 8, 0);

        ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        _icon = new BibleIconView(18)
        {
            Margin = new Thickness(0, 0, 8, 0),
        };

        _refText = new TextBlock
        {
            FontSize = 10.5,
            LineHeight = 13,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
        };

        _favoriteStar = new TextBlock
        {
            Text = "\uEB52", // HeartFill em Segoe Fluent Icons
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 8.5,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(5, 1, 0, 0),
            Visibility = Visibility.Collapsed,
        };

        _refHeaderRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _refHeaderRow.Children.Add(_refText);
        _refHeaderRow.Children.Add(_favoriteStar);

        _verseLine = new TextBlock
        {
            FontSize = 11.5,
            LineHeight = 14.5,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
        };

        _twoLinePanel = new StackPanel
        {
            Spacing = 1,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Stretch,
        };
        _twoLinePanel.Children.Add(_refHeaderRow);
        _twoLinePanel.Children.Add(_verseLine);

        _singleLineText = new TextBlock
        {
            FontSize = 11.5,
            LineHeight = 15,
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
            MaxLines = 1,
            Visibility = Visibility.Collapsed,
        };

        var textHost = new Grid { VerticalAlignment = VerticalAlignment.Center };
        textHost.Children.Add(_twoLinePanel);
        textHost.Children.Add(_singleLineText);

        Children.Add(_icon);
        Grid.SetColumn(textHost, 1);
        Children.Add(textHost);

        SizeChanged += (_, args) =>
        {
            var compact = args.NewSize.Height > 0 && args.NewSize.Height < 34;
            if (compact != _isCompactTaskbar)
            {
                _isCompactTaskbar = compact;
                Render();
            }
        };

        ActualThemeChanged += (_, _) => Render();
        Render();
    }

    public void Update(WidgetSettings settings, DailyVerseSnapshot snapshot)
    {
        _settings = settings;
        _snapshot = snapshot;
        Render();
    }

    private void Render()
    {
        var isLight = ActualTheme == ElementTheme.Light;
        var verse = _snapshot?.Verse;

        _icon.SetWarmAccent(_settings.SubtleWarmAccent);

        if (_settings.DisplayMode == PreviewDisplayMode.IconOnly)
        {
            _icon.Visibility = Visibility.Visible;
            _icon.Margin = new Thickness(0);
            _twoLinePanel.Visibility = Visibility.Collapsed;
            _singleLineText.Visibility = Visibility.Collapsed;
            Padding = new Thickness(4, 0, 4, 0);
            ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinitions[1].Width = new GridLength(0);
        }
        else
        {
            Padding = new Thickness(8, 0, 8, 0);
            ColumnDefinitions[0].Width = GridLength.Auto;
            ColumnDefinitions[1].Width = new GridLength(1, GridUnitType.Star);
            _icon.Visibility = _settings.ShowVerseIcon ? Visibility.Visible : Visibility.Collapsed;
            _icon.Margin = new Thickness(0, 0, 8, 0);

            if (verse is null)
            {
                _twoLinePanel.Visibility = Visibility.Collapsed;
                _singleLineText.Visibility = Visibility.Visible;
                _singleLineText.Text = "Carregando versículo…";
            }
            else if (_settings.DisplayMode == PreviewDisplayMode.ReferenceOnly)
            {
                _twoLinePanel.Visibility = Visibility.Collapsed;
                _singleLineText.Visibility = Visibility.Visible;
                _singleLineText.MaxLines = 1;
                _singleLineText.TextWrapping = TextWrapping.NoWrap;
                _singleLineText.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
                _singleLineText.Text = verse.Reference;
            }
            else if (_settings.DisplayMode == PreviewDisplayMode.VerseOnly)
            {
                _twoLinePanel.Visibility = Visibility.Collapsed;
                _singleLineText.Visibility = Visibility.Visible;
                _singleLineText.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
                if (_isCompactTaskbar)
                {
                    _singleLineText.MaxLines = 1;
                    _singleLineText.TextWrapping = TextWrapping.NoWrap;
                    _singleLineText.FontSize = 11;
                    _singleLineText.LineHeight = 14;
                }
                else
                {
                    _singleLineText.MaxLines = 2;
                    _singleLineText.TextWrapping = TextWrapping.WrapWholeWords;
                    _singleLineText.FontSize = 11;
                    _singleLineText.LineHeight = 13.5;
                }
                _singleLineText.Text = verse.Text;
            }
            else // VerseAndReference (padrão)
            {
                if (_isCompactTaskbar)
                {
                    _twoLinePanel.Visibility = Visibility.Collapsed;
                    _singleLineText.Visibility = Visibility.Visible;
                    _singleLineText.MaxLines = 1;
                    _singleLineText.TextWrapping = TextWrapping.NoWrap;
                    _singleLineText.FontWeight = Microsoft.UI.Text.FontWeights.Normal;
                    _singleLineText.FontSize = 11;
                    _singleLineText.LineHeight = 14;
                    _singleLineText.Text = $"{verse.ShortReference} · {verse.Text}";
                }
                else
                {
                    _singleLineText.Visibility = Visibility.Collapsed;
                    _twoLinePanel.Visibility = Visibility.Visible;
                    _refText.Text = verse.Reference;
                    _verseLine.Text = verse.Text;
                    _favoriteStar.Visibility = _snapshot?.IsFavorite == true
                        ? Visibility.Visible
                        : Visibility.Collapsed;
                }
            }
        }

        if (_settings.SubtleWarmAccent)
        {
            var refColor = isLight
                ? Color.FromArgb(0xFF, 0x9A, 0x5B, 0x13)
                : Color.FromArgb(0xFF, 0xF5, 0xC8, 0x5C);
            _refText.Foreground = new SolidColorBrush(refColor);
            _favoriteStar.Foreground = new SolidColorBrush(isLight
                ? Color.FromArgb(0xFF, 0xDC, 0x26, 0x26)
                : Color.FromArgb(0xFF, 0xFB, 0x71, 0x85));
        }
        else
        {
            _refText.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
            _favoriteStar.Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        }

        _verseLine.Foreground = (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];
        _singleLineText.Foreground = _settings.DisplayMode == PreviewDisplayMode.ReferenceOnly && _settings.SubtleWarmAccent
            ? new SolidColorBrush(isLight ? Color.FromArgb(0xFF, 0x9A, 0x5B, 0x13) : Color.FromArgb(0xFF, 0xF5, 0xC8, 0x5C))
            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];

        var tip = verse is not null
            ? $"{verse.Reference}\n“{verse.Text}”\n\nClique para ler o capítulo, copiar ou sortear outro"
            : "Versículo Diário";
        ToolTipService.SetToolTip(this, tip);
    }
}
