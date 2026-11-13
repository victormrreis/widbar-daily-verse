using System.Globalization;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WidBar.DailyVerse.Core.Models;
using WidBar.DailyVerse.Core.Services;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI;

namespace WidBar.DailyVerse.ExtensionApp.UI;

/// <summary>
/// Painel expandido (Flyout e Tile no Widget Board) sem fundo próprio,
/// respeitando Acrylic/Mica do WidBar com leitor de capítulo completo e favoritos.
/// </summary>
public sealed class VerseFlyoutView : UserControl
{
    private enum SubPage
    {
        Verse = 0,
        Chapter = 1,
        Favorites = 2,
    }

    private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

    private readonly DailyVerseCoordinator _coordinator;
    private readonly BibleRepository _repository;

    private readonly BibleIconView _headerIcon;
    private readonly TextBlock _headerTitle;
    private readonly TextBlock _headerDate;

    // Página 1: Versículo do dia
    private readonly StackPanel _versePage;
    private readonly TextBlock _cardRefText;
    private readonly Button _favoriteButton;
    private readonly FontIcon _favoriteGlyph;
    private readonly Button _copyButton;
    private readonly FontIcon _copyGlyph;
    private readonly TextBlock _copyLabel;
    private readonly TextBlock _verseBodyText;
    private readonly Button _shuffleButton;
    private readonly Button _chapterButton;
    private readonly Button _favoritesNavButton;
    private readonly TextBlock _favoritesNavLabel;
    private readonly Border _noticeBanner;
    private readonly TextBlock _noticeText;
    private readonly TextBlock _footerText;

    // Página 2: Leitor do capítulo
    private readonly Grid _chapterPage;
    private readonly TextBlock _chapterTitle;
    private readonly Button _prevChapterBtn;
    private readonly Button _nextChapterBtn;
    private readonly ScrollViewer _chapterScroll;
    private readonly StackPanel _chapterList;

    // Página 3: Favoritos
    private readonly Grid _favoritesPage;
    private readonly TextBlock _favoritesTitle;
    private readonly StackPanel _favoritesList;

    private WidgetSettings _settings = new();
    private DailyVerseSnapshot _snapshot;
    private SubPage _activePage = SubPage.Verse;
    private string _chapterBookId = "PSA";
    private int _chapterNumber = 23;
    private CancellationTokenSource? _copyFeedbackCts;

    public event Action? ShuffleRequested;
    public event Action<string>? FavoriteToggleRequested;
    public event Action<string>? SelectVerseRequested;

    public VerseFlyoutView(BibleRepository repository, DailyVerseCoordinator coordinator)
    {
        _repository = repository;
        _coordinator = coordinator;
        _snapshot = coordinator.Current;

        Background = null;

        var outerGrid = new Grid
        {
            Padding = new Thickness(18, 16, 18, 14),
            RowSpacing = 12,
            Background = null,
        };
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        outerGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        // ---- Cabeçalho (margem direita livre para a engrenagem do WidBar SDK) ----
        _headerIcon = new BibleIconView(24);
        _headerTitle = new TextBlock
        {
            Text = "Versículo do Dia",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        };
        _headerDate = new TextBlock
        {
            FontSize = 11.5,
            Opacity = 0.68,
        };

        var headerTextStack = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        headerTextStack.Children.Add(_headerTitle);
        headerTextStack.Children.Add(_headerDate);

        var headerRow = new Grid { ColumnSpacing = 10, Margin = new Thickness(0, 0, 32, 0) };
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerRow.Children.Add(_headerIcon);
        Grid.SetColumn(headerTextStack, 1);
        headerRow.Children.Add(headerTextStack);
        outerGrid.Children.Add(headerRow);

        // ---- Página 1: Versículo Principal ----
        _cardRefText = new TextBlock
        {
            FontSize = 13.5,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _favoriteGlyph = new FontIcon
        {
            Glyph = "\uEB51",
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 14,
        };
        _favoriteButton = new Button
        {
            Content = _favoriteGlyph,
            Padding = new Thickness(8, 6, 8, 6),
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
        };
        ToolTipService.SetToolTip(_favoriteButton, "Salvar nos favoritos");
        _favoriteButton.Click += (_, _) => FavoriteToggleRequested?.Invoke(_snapshot.Verse.Key);

        _copyGlyph = new FontIcon
        {
            Glyph = "\uE8C8",
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 13.5,
        };
        _copyLabel = new TextBlock
        {
            Text = "Copiar",
            FontSize = 12,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var copyRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        copyRow.Children.Add(_copyGlyph);
        copyRow.Children.Add(_copyLabel);

        _copyButton = new Button
        {
            Content = copyRow,
            Padding = new Thickness(9, 5, 9, 5),
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
        };
        ToolTipService.SetToolTip(_copyButton, "Copiar versículo");
        _copyButton.Click += (_, _) => CopyVerse(_snapshot.Verse);

        var cardTopBar = new Grid { ColumnSpacing = 6 };
        cardTopBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        cardTopBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cardTopBar.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        cardTopBar.Children.Add(_cardRefText);
        Grid.SetColumn(_favoriteButton, 1);
        cardTopBar.Children.Add(_favoriteButton);
        Grid.SetColumn(_copyButton, 2);
        cardTopBar.Children.Add(_copyButton);

        _verseBodyText = new TextBlock
        {
            FontSize = 15.5,
            LineHeight = 23.5,
            TextWrapping = TextWrapping.Wrap,
            IsTextSelectionEnabled = true,
        };

        var cardBody = new StackPanel { Spacing = 10 };
        cardBody.Children.Add(cardTopBar);
        cardBody.Children.Add(_verseBodyText);

        var heroCard = CreateCard(cardBody, new Thickness(16, 14, 16, 16), cornerRadius: 12);
        var heroScroll = new ScrollViewer
        {
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            MaxHeight = 205,
            Content = heroCard,
        };

        // Botões de ação
        _shuffleButton = CreateActionButton("\uE8B1", "Outro");
        ToolTipService.SetToolTip(_shuffleButton, "Sortear outro versículo agora");
        _shuffleButton.Click += (_, _) => ShuffleRequested?.Invoke();

        _chapterButton = CreateActionButton("\uE82D", "Capítulo");
        ToolTipService.SetToolTip(_chapterButton, "Ler o capítulo completo em contexto");
        _chapterButton.Click += (_, _) => OpenChapterView(_snapshot.Verse.BookId, _snapshot.Verse.Chapter);

        var favNavIcon = new FontIcon
        {
            Glyph = "\uEB52",
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 13,
        };
        _favoritesNavLabel = new TextBlock { Text = "Favoritos", FontSize = 12.5 };
        var favNavContent = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        favNavContent.Children.Add(favNavIcon);
        favNavContent.Children.Add(_favoritesNavLabel);
        _favoritesNavButton = new Button
        {
            Content = favNavContent,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 36,
            CornerRadius = new CornerRadius(8),
        };
        ToolTipService.SetToolTip(_favoritesNavButton, "Ver versículos favoritados");
        _favoritesNavButton.Click += (_, _) => OpenFavoritesView();

        var actionsGrid = new Grid { ColumnSpacing = 8 };
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition());
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition());
        actionsGrid.ColumnDefinitions.Add(new ColumnDefinition());
        actionsGrid.Children.Add(_shuffleButton);
        Grid.SetColumn(_chapterButton, 1);
        actionsGrid.Children.Add(_chapterButton);
        Grid.SetColumn(_favoritesNavButton, 2);
        actionsGrid.Children.Add(_favoritesNavButton);

        _noticeText = new TextBlock { FontSize = 11.5, TextWrapping = TextWrapping.Wrap };
        _noticeBanner = new Border
        {
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(10, 7, 10, 7),
            BorderThickness = new Thickness(1),
            Visibility = Visibility.Collapsed,
            Child = _noticeText,
        };

        _versePage = new StackPanel { Spacing = 10, VerticalAlignment = VerticalAlignment.Top };
        _versePage.Children.Add(heroScroll);
        _versePage.Children.Add(actionsGrid);
        _versePage.Children.Add(_noticeBanner);

        // ---- Página 2: Leitor do Capítulo ----
        _chapterPage = new Grid { RowSpacing = 10, Visibility = Visibility.Collapsed };
        _chapterPage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _chapterPage.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var backFromChapter = CreateBackButton(() => SwitchPage(SubPage.Verse));
        _chapterTitle = new TextBlock
        {
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };

        _prevChapterBtn = CreateMiniIconButton("\uE76B", "Capítulo anterior");
        _prevChapterBtn.Click += (_, _) => OpenChapterView(_chapterBookId, _chapterNumber - 1);

        _nextChapterBtn = CreateMiniIconButton("\uE76C", "Próximo capítulo");
        _nextChapterBtn.Click += (_, _) => OpenChapterView(_chapterBookId, _chapterNumber + 1);

        var chapterNav = new Grid { ColumnSpacing = 6 };
        chapterNav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        chapterNav.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        chapterNav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        chapterNav.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        chapterNav.Children.Add(backFromChapter);
        Grid.SetColumn(_chapterTitle, 1);
        chapterNav.Children.Add(_chapterTitle);
        Grid.SetColumn(_prevChapterBtn, 2);
        chapterNav.Children.Add(_prevChapterBtn);
        Grid.SetColumn(_nextChapterBtn, 3);
        chapterNav.Children.Add(_nextChapterBtn);

        _chapterList = new StackPanel { Spacing = 6, Padding = new Thickness(2, 0, 8, 4) };
        _chapterScroll = new ScrollViewer
        {
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _chapterList,
        };

        _chapterPage.Children.Add(chapterNav);
        Grid.SetRow(_chapterScroll, 1);
        _chapterPage.Children.Add(_chapterScroll);

        // ---- Página 3: Favoritos ----
        _favoritesPage = new Grid { RowSpacing = 10, Visibility = Visibility.Collapsed };
        _favoritesPage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        _favoritesPage.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var backFromFavs = CreateBackButton(() => SwitchPage(SubPage.Verse));
        _favoritesTitle = new TextBlock
        {
            Text = "Favoritos",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            VerticalAlignment = VerticalAlignment.Center,
        };
        var favHeader = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        favHeader.Children.Add(backFromFavs);
        favHeader.Children.Add(_favoritesTitle);

        _favoritesList = new StackPanel { Spacing = 8, Padding = new Thickness(2, 0, 8, 4) };
        var favScroll = new ScrollViewer
        {
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = _favoritesList,
        };

        _favoritesPage.Children.Add(favHeader);
        Grid.SetRow(favScroll, 1);
        _favoritesPage.Children.Add(favScroll);

        // Área central com as 3 subpáginas
        var contentHost = new Grid();
        contentHost.Children.Add(_versePage);
        contentHost.Children.Add(_chapterPage);
        contentHost.Children.Add(_favoritesPage);
        Grid.SetRow(contentHost, 1);
        outerGrid.Children.Add(contentHost);

        // ---- Rodapé ----
        _footerText = new TextBlock
        {
            FontSize = 11,
            Opacity = 0.6,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        Grid.SetRow(_footerText, 2);
        outerGrid.Children.Add(_footerText);

        Content = outerGrid;
        ActualThemeChanged += (_, _) => Render();
        Render();
    }

    public void Update(WidgetSettings settings, DailyVerseSnapshot snapshot)
    {
        _settings = settings;
        _snapshot = snapshot;
        Render();
    }

    public void ResetToMainPage() => SwitchPage(SubPage.Verse);

    private void SwitchPage(SubPage page)
    {
        _activePage = page;
        _versePage.Visibility = page == SubPage.Verse ? Visibility.Visible : Visibility.Collapsed;
        _chapterPage.Visibility = page == SubPage.Chapter ? Visibility.Visible : Visibility.Collapsed;
        _favoritesPage.Visibility = page == SubPage.Favorites ? Visibility.Visible : Visibility.Collapsed;
    }

    private void Render()
    {
        var isLight = ActualTheme == ElementTheme.Light;
        var verse = _snapshot.Verse;

        _headerIcon.SetWarmAccent(_settings.SubtleWarmAccent);

        var formattedDate = _snapshot.Date.ToDateTime(TimeOnly.MinValue).ToString("dddd, d 'de' MMMM", PtBr);
        _headerDate.Text = char.ToUpper(formattedDate[0], PtBr) + formattedDate[1..];

        _cardRefText.Text = verse.Reference;
        _cardRefText.Foreground = _settings.SubtleWarmAccent
            ? new SolidColorBrush(isLight ? Color.FromArgb(0xFF, 0x9A, 0x5B, 0x13) : Color.FromArgb(0xFF, 0xF5, 0xC8, 0x5C))
            : (Brush)Application.Current.Resources["TextFillColorPrimaryBrush"];

        _verseBodyText.Text = $"“{verse.Text}”";

        _favoriteGlyph.Glyph = _snapshot.IsFavorite ? "\uEB52" : "\uEB51";
        _favoriteGlyph.Foreground = _snapshot.IsFavorite
            ? new SolidColorBrush(isLight ? Color.FromArgb(0xFF, 0xDC, 0x26, 0x26) : Color.FromArgb(0xFF, 0xFB, 0x71, 0x85))
            : (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"];
        ToolTipService.SetToolTip(_favoriteButton, _snapshot.IsFavorite ? "Remover dos favoritos" : "Salvar nos favoritos");

        _favoritesNavLabel.Text = _snapshot.FavoriteCount > 0 ? $"Favoritos ({_snapshot.FavoriteCount})" : "Favoritos";

        if (!string.IsNullOrWhiteSpace(_snapshot.WarningMessage))
        {
            _noticeBanner.Visibility = Visibility.Visible;
            _noticeBanner.Background = new SolidColorBrush(Color.FromArgb(0x22, 0xEF, 0x44, 0x44));
            _noticeBanner.BorderBrush = new SolidColorBrush(Color.FromArgb(0x55, 0xEF, 0x44, 0x44));
            _noticeText.Text = _snapshot.WarningMessage;
        }
        else if (_snapshot.FallbackFromEmptyFavorites)
        {
            _noticeBanner.Visibility = Visibility.Visible;
            _noticeBanner.Background = new SolidColorBrush(Color.FromArgb(0x1E, 0xF5, 0x9E, 0x0B));
            _noticeBanner.BorderBrush = new SolidColorBrush(Color.FromArgb(0x4A, 0xF5, 0x9E, 0x0B));
            _noticeText.Text = "Você ainda não salvou favoritos — exibindo da Seleção Devocional.";
        }
        else
        {
            _noticeBanner.Visibility = Visibility.Collapsed;
        }

        var poolName = _settings.PoolMode switch
        {
            VersePoolMode.Devotional => "Seleção Devocional",
            VersePoolMode.PsalmsProverbsNewTestament => "Salmos, Provérbios e NT",
            VersePoolMode.FullBible => "Bíblia Completa (66 livros)",
            VersePoolMode.FavoritesOnly => "Meus Favoritos",
            _ => "Seleção Devocional",
        };
        _footerText.Text = $"{poolName} · Bíblia Livre (CC BY 4.0)";

        if (_activePage == SubPage.Chapter)
        {
            PopulateChapterList();
        }
        else if (_activePage == SubPage.Favorites)
        {
            PopulateFavoritesList();
        }
    }

    private void OpenChapterView(string bookId, int chapter)
    {
        var maxChapters = _repository.GetChapterCount(bookId);
        if (chapter < 1 || chapter > maxChapters) return;

        _chapterBookId = bookId;
        _chapterNumber = chapter;
        SwitchPage(SubPage.Chapter);
        PopulateChapterList();
    }

    private void PopulateChapterList()
    {
        var isLight = ActualTheme == ElementTheme.Light;
        var verses = _coordinator.GetChapter(_chapterBookId, _chapterNumber);
        var maxChapters = _repository.GetChapterCount(_chapterBookId);

        _chapterTitle.Text = verses.Count > 0 ? $"{verses[0].BookName} {_chapterNumber}" : $"Capítulo {_chapterNumber}";
        _prevChapterBtn.IsEnabled = _chapterNumber > 1;
        _nextChapterBtn.IsEnabled = _chapterNumber < maxChapters;

        _chapterList.Children.Clear();
        FrameworkElement? highlightedRow = null;

        foreach (var v in verses)
        {
            var isCurrent = string.Equals(v.Key, _snapshot.Verse.Key, StringComparison.Ordinal);

            var numBlock = new TextBlock
            {
                Text = v.Verse.ToString(PtBr),
                FontSize = 11,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                Width = 22,
                Opacity = isCurrent ? 1.0 : 0.55,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 2, 0, 0),
            };
            if (isCurrent)
            {
                numBlock.Foreground = new SolidColorBrush(isLight
                    ? Color.FromArgb(0xFF, 0x9A, 0x5B, 0x13)
                    : Color.FromArgb(0xFF, 0xF5, 0xC8, 0x5C));
            }

            var textBlock = new TextBlock
            {
                Text = v.Text,
                FontSize = 13,
                LineHeight = 19,
                TextWrapping = TextWrapping.Wrap,
                FontWeight = isCurrent ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal,
                IsTextSelectionEnabled = true,
            };

            var row = new Grid { ColumnSpacing = 6 };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.Children.Add(numBlock);
            Grid.SetColumn(textBlock, 1);
            row.Children.Add(textBlock);

            var container = new Border
            {
                Padding = new Thickness(8, 6, 8, 6),
                CornerRadius = new CornerRadius(7),
                BorderThickness = new Thickness(isCurrent ? 1 : 0),
                Child = row,
            };

            if (isCurrent)
            {
                container.Background = new SolidColorBrush(isLight
                    ? Color.FromArgb(0x1E, 0xD9, 0x77, 0x06)
                    : Color.FromArgb(0x20, 0xF5, 0xC8, 0x5C));
                container.BorderBrush = new SolidColorBrush(isLight
                    ? Color.FromArgb(0x55, 0xD9, 0x77, 0x06)
                    : Color.FromArgb(0x50, 0xF5, 0xC8, 0x5C));
                highlightedRow = container;
            }

            _chapterList.Children.Add(container);
        }

        if (highlightedRow is not null)
        {
            DispatcherQueue.TryEnqueue(() => highlightedRow.StartBringIntoView());
        }
        else
        {
            _chapterScroll.ChangeView(null, 0, null, disableAnimation: true);
        }
    }

    private void OpenFavoritesView()
    {
        SwitchPage(SubPage.Favorites);
        PopulateFavoritesList();
    }

    private void PopulateFavoritesList()
    {
        var favs = _coordinator.GetFavorites();
        _favoritesTitle.Text = $"Favoritos ({favs.Count})";
        _favoritesList.Children.Clear();

        if (favs.Count == 0)
        {
            _favoritesList.Children.Add(new TextBlock
            {
                Text = "Nenhum versículo favoritado ainda.\nToque no ícone de coração no versículo do dia para guardar seus preferidos.",
                FontSize = 12.5,
                Opacity = 0.68,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(4, 16, 4, 0),
            });
            return;
        }

        foreach (var v in favs)
        {
            var refBlock = new TextBlock
            {
                Text = v.Reference,
                FontSize = 12.5,
                FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
                VerticalAlignment = VerticalAlignment.Center,
            };

            var pinBtn = CreateMiniIconButton("\uE8AC", "Exibir na barra hoje");
            pinBtn.Click += (_, _) =>
            {
                SelectVerseRequested?.Invoke(v.Key);
                SwitchPage(SubPage.Verse);
            };

            var copyBtn = CreateMiniIconButton("\uE8C8", "Copiar versículo");
            copyBtn.Click += (_, _) => CopyVerse(v);

            var removeBtn = CreateMiniIconButton("\uE74D", "Remover dos favoritos");
            removeBtn.Click += (_, _) => FavoriteToggleRequested?.Invoke(v.Key);

            var top = new Grid { ColumnSpacing = 4 };
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            top.Children.Add(refBlock);
            Grid.SetColumn(pinBtn, 1);
            top.Children.Add(pinBtn);
            Grid.SetColumn(copyBtn, 2);
            top.Children.Add(copyBtn);
            Grid.SetColumn(removeBtn, 3);
            top.Children.Add(removeBtn);

            var excerpt = new TextBlock
            {
                Text = v.Text,
                FontSize = 12,
                Opacity = 0.82,
                TextWrapping = TextWrapping.Wrap,
                MaxLines = 3,
                TextTrimming = TextTrimming.CharacterEllipsis,
            };

            var cardStack = new StackPanel { Spacing = 4 };
            cardStack.Children.Add(top);
            cardStack.Children.Add(excerpt);

            _favoritesList.Children.Add(CreateCard(cardStack, new Thickness(12, 9, 10, 10), cornerRadius: 8));
        }
    }

    private void CopyVerse(BibleVerse verse)
    {
        var dp = new DataPackage();
        dp.SetText($"\"{verse.Text}\"\n— {verse.Reference} (Bíblia Livre)");
        Clipboard.SetContent(dp);
        Clipboard.Flush();

        _copyFeedbackCts?.Cancel();
        _copyFeedbackCts?.Dispose();
        _copyFeedbackCts = new CancellationTokenSource();
        var token = _copyFeedbackCts.Token;

        _copyGlyph.Glyph = "\uE73E";
        _copyLabel.Text = "Copiado";

        _ = ResetCopyFeedbackAsync(token);
    }

    private async Task ResetCopyFeedbackAsync(CancellationToken token)
    {
        try
        {
            await Task.Delay(1800, token);
            _copyGlyph.Glyph = "\uE8C8";
            _copyLabel.Text = "Copiar";
        }
        catch (OperationCanceledException) { }
    }

    private static Button CreateActionButton(string glyph, string label)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
        stack.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 13,
        });
        stack.Children.Add(new TextBlock { Text = label, FontSize = 12.5 });

        return new Button
        {
            Content = stack,
            HorizontalAlignment = HorizontalAlignment.Stretch,
            Height = 36,
            CornerRadius = new CornerRadius(8),
        };
    }

    private static Button CreateBackButton(Action onClick)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        stack.Children.Add(new FontIcon
        {
            Glyph = "\uE72B",
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 12,
        });
        stack.Children.Add(new TextBlock { Text = "Voltar", FontSize = 12 });

        var btn = new Button
        {
            Content = stack,
            Padding = new Thickness(9, 5, 10, 5),
            CornerRadius = new CornerRadius(6),
        };
        btn.Click += (_, _) => onClick();
        return btn;
    }

    private static Button CreateMiniIconButton(string glyph, string tooltip)
    {
        var btn = new Button
        {
            Content = new FontIcon
            {
                Glyph = glyph,
                FontFamily = new FontFamily("Segoe Fluent Icons"),
                FontSize = 12,
            },
            Padding = new Thickness(7, 5, 7, 5),
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Colors.Transparent),
            BorderThickness = new Thickness(0),
        };
        ToolTipService.SetToolTip(btn, tooltip);
        return btn;
    }

    private static Border CreateCard(UIElement child, Thickness padding, double cornerRadius) => new()
    {
        Child = child,
        Padding = padding,
        CornerRadius = new CornerRadius(cornerRadius),
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
    };
}
