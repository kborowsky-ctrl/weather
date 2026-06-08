using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WeatherWizard.Services;
using Windows.Foundation;

namespace WeatherWizard.Views;

/// <summary>
/// Forecast weather column: full text when it fits in two lines; otherwise the shortest abbreviation that fits.
/// </summary>
public sealed class AdaptiveForecastConditionsTextBlock : UserControl
{
    private const double FontSizePx = 11;
    private const int MaxLines = 2;

    private readonly TextBlock _text;
    private double _lineHeight = 18;
    private string _fullText = "";
    private int _weatherCode = -1;
    private string _displayedText = "";

    public static readonly DependencyProperty FullTextProperty =
        DependencyProperty.Register(
            nameof(FullText),
            typeof(string),
            typeof(AdaptiveForecastConditionsTextBlock),
            new PropertyMetadata(string.Empty, OnDisplayInputChanged));

    public static readonly DependencyProperty WeatherCodeProperty =
        DependencyProperty.Register(
            nameof(WeatherCode),
            typeof(int),
            typeof(AdaptiveForecastConditionsTextBlock),
            new PropertyMetadata(-1, OnDisplayInputChanged));

    public string FullText
    {
        get => (string)GetValue(FullTextProperty);
        set => SetValue(FullTextProperty, value);
    }

    public int WeatherCode
    {
        get => (int)GetValue(WeatherCodeProperty);
        set => SetValue(WeatherCodeProperty, value);
    }

    public AdaptiveForecastConditionsTextBlock()
    {
        if (Application.Current.Resources["AppBodyTextBrush"] is Brush fg)
            Foreground = fg;

        _text = new TextBlock
        {
            FontSize = FontSizePx,
            TextWrapping = TextWrapping.WrapWholeWords,
            VerticalAlignment = VerticalAlignment.Center,
        };

        if (Foreground is Brush textFg)
            _text.Foreground = textFg;

        Content = _text;
        HorizontalAlignment = HorizontalAlignment.Stretch;
        VerticalAlignment = VerticalAlignment.Center;

        Loaded += (_, _) => UpdateDisplayText();
        SizeChanged += (_, _) => UpdateDisplayText();
    }

    private static void OnDisplayInputChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is AdaptiveForecastConditionsTextBlock self)
            self.UpdateDisplayText();
    }

    private void UpdateDisplayText()
    {
        _fullText = FullText ?? string.Empty;
        _weatherCode = WeatherCode;

        var width = ActualWidth;
        if (width <= 0 || double.IsNaN(width))
        {
            SetDisplayedText(_fullText);
            return;
        }

        MeasureLineHeight(width);

        var chosen = _fullText;
        foreach (var candidate in ForecastConditionsAbbreviator.GetCandidates(_fullText, _weatherCode))
        {
            chosen = candidate;
            if (TextFits(candidate, width))
                break;
        }

        SetDisplayedText(chosen);
    }

    private void MeasureLineHeight(double width)
    {
        _text.Text = "Ag";
        _text.MaxLines = 1;
        _text.MaxWidth = width;
        _text.Measure(new Size(width, double.PositiveInfinity));
        if (_text.DesiredSize.Height > 0)
            _lineHeight = _text.DesiredSize.Height;
    }

    private bool TextFits(string text, double width)
    {
        if (string.IsNullOrEmpty(text))
            return true;

        _text.MaxLines = 0;
        _text.MaxWidth = width;
        _text.Text = text;
        _text.Measure(new Size(width, double.PositiveInfinity));

        var maxHeight = _lineHeight * MaxLines + 0.5;
        return _text.DesiredSize.Height <= maxHeight;
    }

    private void SetDisplayedText(string text)
    {
        if (string.Equals(_displayedText, text, StringComparison.Ordinal))
            return;

        _displayedText = text;
        _text.MaxLines = MaxLines;
        _text.MaxWidth = double.PositiveInfinity;
        _text.Text = text;

        if (!string.IsNullOrEmpty(_fullText)
            && !string.Equals(text, _fullText.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            ToolTipService.SetToolTip(this, _fullText);
        }
        else
        {
            ToolTipService.SetToolTip(this, null);
        }
    }
}
