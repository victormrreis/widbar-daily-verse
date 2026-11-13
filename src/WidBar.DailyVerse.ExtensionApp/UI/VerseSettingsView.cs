using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WidBar.DailyVerse.Core.Models;
using WidBar.DailyVerse.Core.Services;
using WidBar.SDK;
using Windows.UI;

namespace WidBar.DailyVerse.ExtensionApp.UI;

/// <summary>
/// Página de configurações hospedada pelo WidBar SDK (Salvar / Cancelar),
/// com prévia ao vivo da barra de tarefas.
/// </summary>
public sealed class VerseSettingsView : UserControl
{
    public VerseSettingsView(
        IWidgetSettingsContext context,
        BibleRepository repository,
        DailyVerseCoordinator coordinator)
    {
        Background = null;
        var draft = WidgetSettings.FromJson(context.SettingsJson);
        var page = new StackPanel { Spacing = 6, Padding = new Thickness(24, 12, 24, 24) };

        // ---- Prévia ao vivo ----
        var livePreview = new TaskbarPreviewControl { Height = 44 };
        var widthText = Muted("");
        var previewHost = new Border
        {
            MinHeight = 56,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(Color.FromArgb(0x55, 0x00, 0x00, 0x00)),
            Padding = new Thickness(12, 6, 12, 6),
            HorizontalAlignment = HorizontalAlignment.Center,
            Child = livePreview,
        };

        var widthValueText = new TextBlock
        {
            FontSize = 12,
            Opacity = 0.72,
            VerticalAlignment = VerticalAlignment.Center,
            Width = 52,
            TextAlignment = TextAlignment.Right,
        };

        void RefreshPreview()
        {
            var width = draft.ComputePreviewWidth();
            livePreview.Width = width;
            livePreview.Update(draft, coordinator.Current);
            widthText.Text = $"Largura: {width} px";
            widthValueText.Text = $"{width} px";
        }

        void Changed()
        {
            draft.Normalize();
            context.SaveSettings(draft.ToJson());
            context.RequestPreviewRefresh();
            RefreshPreview();
        }

        var previewHeader = new Grid();
        previewHeader.ColumnDefinitions.Add(new ColumnDefinition());
        previewHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var titleCol = new StackPanel { Spacing = 2 };
        titleCol.Children.Add(new TextBlock
        {
            Text = "Prévia na barra de tarefas",
            FontSize = 16,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
        });
        titleCol.Children.Add(Muted("Clique no widget na barra para ler completo, ver o capítulo, favoritar ou sortear outro."));
        previewHeader.Children.Add(titleCol);
        Grid.SetColumn(widthText, 1);
        widthText.VerticalAlignment = VerticalAlignment.Top;
        previewHeader.Children.Add(widthText);

        var previewBody = new StackPanel { Spacing = 12 };
        previewBody.Children.Add(previewHeader);
        previewBody.Children.Add(previewHost);
        page.Children.Add(CreateCard(previewBody));

        // ---- Seção: Aparência ----
        page.Children.Add(Section("Barra de Tarefas", "Personalize como o versículo aparece na barra do Windows 11."));

        PreviewDisplayMode[] displayModes =
        [
            PreviewDisplayMode.VerseAndReference,
            PreviewDisplayMode.VerseOnly,
            PreviewDisplayMode.ReferenceOnly,
            PreviewDisplayMode.IconOnly,
        ];
        string[] displayLabels =
        [
            "Referência + Versículo (padrão)",
            "Somente versículo (até 2 linhas)",
            "Compacto (somente referência)",
            "Minimalista (somente ícone)",
        ];

        var modeCombo = new ComboBox { MinWidth = 230 };
        foreach (var l in displayLabels) modeCombo.Items.Add(l);
        modeCombo.SelectedIndex = Math.Max(0, Array.IndexOf(displayModes, draft.DisplayMode));

        var widthSlider = new Slider
        {
            Minimum = 180,
            Maximum = 460,
            StepFrequency = 10,
            Value = draft.PreviewWidth,
            Width = 170,
            IsEnabled = draft.DisplayMode is PreviewDisplayMode.VerseAndReference or PreviewDisplayMode.VerseOnly,
        };

        modeCombo.SelectionChanged += (_, _) =>
        {
            if (modeCombo.SelectedIndex < 0) return;
            draft.DisplayMode = displayModes[modeCombo.SelectedIndex];
            widthSlider.IsEnabled = draft.DisplayMode is PreviewDisplayMode.VerseAndReference or PreviewDisplayMode.VerseOnly;
            Changed();
        };
        page.Children.Add(Row("\uE8A9", "Modo de exibição", "Escolha entre texto completo na barra ou visual compacto.", modeCombo));

        widthSlider.ValueChanged += (_, e) =>
        {
            draft.PreviewWidth = (int)Math.Round(e.NewValue);
            Changed();
        };
        var sliderRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        sliderRow.Children.Add(widthSlider);
        sliderRow.Children.Add(widthValueText);
        page.Children.Add(Row("\uE76F", "Largura na barra", "Ajuste quanto espaço o texto do versículo ocupa na barra.", sliderRow));

        var iconSwitch = new ToggleSwitch { IsOn = draft.ShowVerseIcon, OnContent = "Sim", OffContent = "Não", MinWidth = 0 };
        iconSwitch.Toggled += (_, _) => { draft.ShowVerseIcon = iconSwitch.IsOn; Changed(); };
        page.Children.Add(Row("\uE82D", "Exibir ícone da Bíblia", "Mostra o ícone de livro aberto ao lado do versículo.", iconSwitch));

        var accentSwitch = new ToggleSwitch { IsOn = draft.SubtleWarmAccent, OnContent = "Sim", OffContent = "Não", MinWidth = 0 };
        accentSwitch.Toggled += (_, _) => { draft.SubtleWarmAccent = accentSwitch.IsOn; Changed(); };
        page.Children.Add(Row("\uE790", "Destaque dourado sutil", "Aplica tom âmbar/dourado discreto no ícone e na referência.", accentSwitch));

        // ---- Seção: Sorteio Diário ----
        page.Children.Add(Section("Sorteio Diário", "Defina quais livros entram no sorteio automático de cada dia."));

        VersePoolMode[] poolModes =
        [
            VersePoolMode.Devotional,
            VersePoolMode.PsalmsProverbsNewTestament,
            VersePoolMode.FullBible,
            VersePoolMode.FavoritesOnly,
        ];
        string[] poolLabels =
        [
            $"Seleção Devocional ({repository.CuratedVerseCount} versículos)",
            "Salmos, Provérbios e Novo Testamento",
            $"Bíblia Completa ({repository.TotalVerseCount:N0} versículos)",
            "Somente meus Favoritos",
        ];

        var poolCombo = new ComboBox { MinWidth = 240 };
        foreach (var l in poolLabels) poolCombo.Items.Add(l);
        poolCombo.SelectedIndex = Math.Max(0, Array.IndexOf(poolModes, draft.PoolMode));
        poolCombo.SelectionChanged += (_, _) =>
        {
            if (poolCombo.SelectedIndex < 0) return;
            draft.PoolMode = poolModes[poolCombo.SelectedIndex];
            Changed();
        };
        page.Children.Add(Row("\uE8F1", "Coleção do sorteio", "Aplica no próximo sorteio automático ou ao clicar em Sortear abaixo.", poolCombo));

        var shuffleBtn = new Button { Content = "Sortear novo versículo agora", CornerRadius = new CornerRadius(6) };
        shuffleBtn.Click += (_, _) =>
        {
            coordinator.ShuffleNow(draft.PoolMode);
            RefreshPreview();
        };
        page.Children.Add(Row("\uE8B1", "Trocar versículo de hoje", "Mantém a rotação diária automática à meia-noite.", shuffleBtn));

        RefreshPreview();
        Content = new ScrollViewer
        {
            Background = null,
            HorizontalScrollMode = ScrollMode.Disabled,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            Content = page,
        };
    }

    private static TextBlock Muted(string text) =>
        new() { Text = text, FontSize = 12, Opacity = 0.65, TextWrapping = TextWrapping.Wrap };

    private static StackPanel Section(string title, string subtitle)
    {
        var p = new StackPanel { Spacing = 2, Margin = new Thickness(2, 14, 0, 4) };
        p.Children.Add(new TextBlock { Text = title, FontSize = 15, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold });
        p.Children.Add(Muted(subtitle));
        return p;
    }

    private static Border Row(string glyph, string title, string subtitle, FrameworkElement control)
    {
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        g.Children.Add(new FontIcon
        {
            Glyph = glyph,
            FontFamily = new FontFamily("Segoe Fluent Icons"),
            FontSize = 16,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var text = new StackPanel { Spacing = 1, VerticalAlignment = VerticalAlignment.Center };
        text.Children.Add(new TextBlock { Text = title, TextWrapping = TextWrapping.Wrap });
        text.Children.Add(Muted(subtitle));
        Grid.SetColumn(text, 1);
        g.Children.Add(text);

        control.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetColumn(control, 2);
        g.Children.Add(control);

        return CreateCard(g, new Thickness(14, 10, 14, 10));
    }

    private static Border CreateCard(UIElement child, Thickness? padding = null) => new()
    {
        Child = child,
        Padding = padding ?? new Thickness(14),
        CornerRadius = new CornerRadius(8),
        Background = (Brush)Application.Current.Resources["CardBackgroundFillColorDefaultBrush"],
        BorderBrush = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
        BorderThickness = new Thickness(1),
    };
}
