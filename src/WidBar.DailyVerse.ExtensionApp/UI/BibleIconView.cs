using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using PathShape = Microsoft.UI.Xaml.Shapes.Path;

namespace WidBar.DailyVerse.ExtensionApp.UI;

/// <summary>
/// Ícone vetorial limpo de Bíblia aberta (grade 24x24), adaptável aos temas Claro e Escuro da barra de tarefas.
/// </summary>
public sealed class BibleIconView : UserControl
{
    private readonly PathShape _pagesFill;
    private readonly PathShape _pagesOutline;
    private readonly PathShape _pageLines;
    private readonly PathShape _bookmark;
    private bool _warmAccent = true;

    public BibleIconView(double size = 18)
    {
        Width = size;
        Height = size;
        HorizontalAlignment = HorizontalAlignment.Center;
        VerticalAlignment = VerticalAlignment.Center;

        var canvas = new Canvas { Width = 24, Height = 24 };

        const string pagesData =
            "M 2.5,5.8 C 2.5,5.1 3.1,4.6 3.8,4.6 L 9.4,4.6 C 10.9,4.6 12.0,5.6 12.0,7.0 " +
            "C 12.0,5.6 13.1,4.6 14.6,4.6 L 20.2,4.6 C 20.9,4.6 21.5,5.1 21.5,5.8 L 21.5,17.2 " +
            "C 21.5,17.9 20.9,18.4 20.2,18.4 L 14.8,18.4 C 13.6,18.4 12.5,18.9 12.0,19.7 " +
            "C 11.5,18.9 10.4,18.4 9.2,18.4 L 3.8,18.4 C 3.1,18.4 2.5,17.9 2.5,17.2 Z";

        _pagesFill = CreatePath(pagesData, strokeThickness: 0);
        _pagesOutline = CreatePath(pagesData + " M 12.0,7.0 L 12.0,19.7", strokeThickness: 1.7);
        _pageLines = CreatePath(
            "M 5.2,8.6 L 9.2,8.6 M 5.2,11.6 L 9.2,11.6 M 5.2,14.6 L 8.4,14.6 " +
            "M 14.8,8.6 L 18.8,8.6 M 14.8,11.6 L 18.8,11.6 M 14.8,14.6 L 18.0,14.6",
            strokeThickness: 1.45);
        _bookmark = CreatePath("M 15.8,4.6 L 15.8,9.8 L 17.2,8.5 L 18.6,9.8 L 18.6,4.6", strokeThickness: 1.35);

        canvas.Children.Add(_pagesFill);
        canvas.Children.Add(_pagesOutline);
        canvas.Children.Add(_pageLines);
        canvas.Children.Add(_bookmark);

        Content = new Viewbox
        {
            Stretch = Stretch.Uniform,
            Child = canvas,
        };

        ActualThemeChanged += (_, _) => UpdateBrushes();
        UpdateBrushes();
    }

    public void SetWarmAccent(bool warmAccent)
    {
        if (_warmAccent == warmAccent) return;
        _warmAccent = warmAccent;
        UpdateBrushes();
    }

    private void UpdateBrushes()
    {
        var isLight = ActualTheme == ElementTheme.Light;

        var primaryStroke = _warmAccent
            ? (isLight ? Color.FromArgb(0xFF, 0x9A, 0x5B, 0x13) : Color.FromArgb(0xFF, 0xF5, 0xC8, 0x5C))
            : (isLight ? Color.FromArgb(0xE8, 0x1F, 0x23, 0x28) : Color.FromArgb(0xF0, 0xF3, 0xF4, 0xF6));

        var secondaryStroke = _warmAccent
            ? (isLight ? Color.FromArgb(0xA6, 0x9A, 0x5B, 0x13) : Color.FromArgb(0xAA, 0xF5, 0xC8, 0x5C))
            : (isLight ? Color.FromArgb(0x90, 0x3A, 0x3F, 0x47) : Color.FromArgb(0x99, 0xD1, 0xD5, 0xDB));

        var fill = _warmAccent
            ? (isLight ? Color.FromArgb(0x1E, 0xD9, 0x77, 0x06) : Color.FromArgb(0x22, 0xF5, 0xC8, 0x5C))
            : (isLight ? Color.FromArgb(0x12, 0x00, 0x00, 0x00) : Color.FromArgb(0x16, 0xFF, 0xFF, 0xFF));

        var ribbonColor = _warmAccent
            ? (isLight ? Color.FromArgb(0xFF, 0xB4, 0x53, 0x09) : Color.FromArgb(0xFF, 0xFB, 0xBF, 0x24))
            : primaryStroke;

        _pagesFill.Fill = new SolidColorBrush(fill);
        _pagesOutline.Stroke = new SolidColorBrush(primaryStroke);
        _pageLines.Stroke = new SolidColorBrush(secondaryStroke);
        _bookmark.Stroke = new SolidColorBrush(ribbonColor);
        _bookmark.Fill = new SolidColorBrush(Color.FromArgb(0x40, ribbonColor.R, ribbonColor.G, ribbonColor.B));
    }

    private static PathShape CreatePath(string geometryData, double strokeThickness)
    {
        var xaml = $"<Geometry xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>{geometryData}</Geometry>";
        return new PathShape
        {
            Data = (Geometry)XamlReader.Load(xaml),
            StrokeThickness = strokeThickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
    }
}
