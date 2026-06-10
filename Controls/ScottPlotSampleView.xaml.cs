using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScottPlot;
using ScottPlotColor = ScottPlot.Color;
using WpfColor = System.Windows.Media.Color;
using WpfColors = System.Windows.Media.Colors;

namespace CMS5000.Controls;

public partial class ScottPlotSampleView : UserControl
{
    public static readonly DependencyProperty PlotTypeProperty =
        DependencyProperty.Register(
            nameof(PlotType), typeof(string), typeof(ScottPlotSampleView),
            new PropertyMetadata("Spectrum", OnPlotTypeChanged));

    public static readonly DependencyProperty EquipmentNameProperty =
        DependencyProperty.Register(
            nameof(EquipmentName), typeof(string), typeof(ScottPlotSampleView),
            new PropertyMetadata("Fan D2", OnEquipmentNameChanged));

    private static readonly ScottPlotColor FigureBackground = ScottPlotColor.FromHex("#101018");
    private static readonly ScottPlotColor DataBackground = ScottPlotColor.FromHex("#141420");
    private static readonly ScottPlotColor AxisText = ScottPlotColor.FromHex("#D8DCE8");
    private static readonly ScottPlotColor GridLine = ScottPlotColor.FromHex("#34384A");
    private static readonly ScottPlotColor LegendBackground = ScottPlotColor.FromHex("#1B1D2A").WithAlpha(0.92);

    private bool _is3DMode;

    public string PlotType
    {
        get => (string)GetValue(PlotTypeProperty);
        set => SetValue(PlotTypeProperty, value);
    }

    public string EquipmentName
    {
        get => (string)GetValue(EquipmentNameProperty);
        set => SetValue(EquipmentNameProperty, value);
    }

    private bool Supports3D => PlotType is "Waterfall" or "Cascade";
    private string EquipmentLabel => string.IsNullOrWhiteSpace(EquipmentName) ? "Equipment" : EquipmentName;

    public ScottPlotSampleView()
    {
        try
        {
            InitializeComponent();
        }
        catch (Exception ex)
        {
            Content = MakeErrorBlock(ex.Message);
            return;
        }

        Loaded += (_, _) =>
        {
            try { RenderChart(); }
            catch (Exception ex) { Content = MakeErrorBlock(ex.Message); }
        };
    }

    private static TextBlock MakeErrorBlock(string msg) => new()
    {
        Text = $"ScottPlot 초기화 실패:\n{msg}",
        TextWrapping = TextWrapping.Wrap,
        Foreground = new SolidColorBrush(WpfColor.FromRgb(0xFF, 0x88, 0x44)),
        VerticalAlignment = System.Windows.VerticalAlignment.Center,
        HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
        Margin = new Thickness(20),
    };

    private static void OnPlotTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScottPlotSampleView v || !v.IsLoaded) return;
        v._is3DMode = false;
        try { v.RenderChart(); } catch { }
    }

    private static void OnEquipmentNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScottPlotSampleView v || !v.IsLoaded) return;
        try { v.RenderChart(); } catch { }
    }

    private void OnToggle2D(object sender, RoutedEventArgs e)
    {
        if (!_is3DMode) return;
        _is3DMode = false;
        RenderChart();
    }

    private void OnToggle3D(object sender, RoutedEventArgs e)
    {
        if (_is3DMode) return;
        _is3DMode = true;
        RenderChart();
    }

    private void RenderChart()
    {
        var type = string.IsNullOrWhiteSpace(PlotType) ? "Spectrum" : PlotType;
        var plot = PlotView.Plot;

        ResetPlot(plot);
        UpdateDimToggle();

        if (_is3DMode && type is "Waterfall" or "Cascade")
            RenderWaterfallMap(plot, type == "Cascade");
        else if (type == "Polar")
            RenderPolar(plot);
        else if (type == "Surface")
            RenderSurfaceHeatmap(plot);
        else if (type == "Campbell Diagram")
            RenderCampbellDiagram(plot);
        else if (type == "Spectrogram")
            RenderSpectrogram(plot);
        else
            RenderXY(plot, type);

        PlotView.Refresh();
    }

    private static void ResetPlot(Plot plot)
    {
        plot.Clear();
        plot.ShowAxesAndGrid();
        plot.Legend.IsVisible = false;

        plot.SetStyle(new PlotStyle
        {
            FigureBackgroundColor = FigureBackground,
            DataBackgroundColor = DataBackground,
            AxisColor = AxisText,
            GridMajorLineColor = GridLine,
            LegendBackgroundColor = LegendBackground,
            LegendFontColor = AxisText,
            LegendOutlineColor = GridLine,
        });

        plot.Grid.MajorLineColor = GridLine;
        plot.Grid.MinorLineColor = GridLine.WithAlpha(0.35);
        plot.Grid.MajorLineWidth = 1;
        plot.Axes.Color(AxisText);
    }

    private void UpdateDimToggle()
    {
        DimToggle.Visibility = Supports3D ? Visibility.Visible : Visibility.Collapsed;

        var blue = new SolidColorBrush(WpfColor.FromRgb(0x29, 0x79, 0xFF));
        var dimBg = new SolidColorBrush(WpfColor.FromRgb(0x2A, 0x2A, 0x3E));
        var white = new SolidColorBrush(WpfColors.White);
        var dimFg = new SolidColorBrush(WpfColor.FromRgb(0x88, 0x88, 0x99));

        Btn2D.Background = _is3DMode ? dimBg : blue;
        Btn2D.Foreground = _is3DMode ? dimFg : white;
        Btn3D.Background = _is3DMode ? blue : dimBg;
        Btn3D.Foreground = _is3DMode ? white : dimFg;
    }

    private void RenderXY(Plot plot, string type)
    {
        var series = type switch
        {
            "Spectrum" => CreateSpectrumSeries(),
            "Waterfall" => CreateWaterfallSeries(false),
            "Cascade" => CreateWaterfallSeries(true),
            "Orbit" => CreateOrbitSeries(),
            "Orbit & Time Base" => CreateOrbitTimeBaseSeries(),
            "Time Base" => CreateTimeBaseSeries(),
            "Bode" => CreateBodeSeries(),
            _ => CreateTrendSeries(),
        };

        foreach (var item in series)
            AddLine(plot, item);

        ApplyLabels(plot, $"{type} - {EquipmentLabel}", GetXLabel(type), GetYLabel(type), showLegend: true);
        ApplyLimits(plot, type);
    }

    private static string GetXLabel(string type) => type switch
    {
        "Spectrum" or "Waterfall" or "Cascade" or "Bode" => "Frequency",
        "Orbit" => "X probe",
        _ => "Time",
    };

    private static string GetYLabel(string type) => type switch
    {
        "Orbit" => "Y probe",
        "Bode" => "Magnitude / Phase",
        _ => "Amplitude",
    };

    private static void ApplyLabels(Plot plot, string title, string xLabel, string yLabel, bool showLegend)
    {
        plot.Title(title);
        plot.XLabel(xLabel);
        plot.YLabel(yLabel);

        if (!showLegend) return;

        var legend = plot.ShowLegend(Alignment.UpperRight);
        legend.BackgroundColor = LegendBackground;
        legend.FontColor = AxisText;
        legend.OutlineColor = GridLine;
    }

    private static void ApplyLimits(Plot plot, string type)
    {
        if (type == "Orbit")
        {
            plot.Axes.SetLimits(-1.4, 1.4, -1.4, 1.4);
            plot.Axes.SquareUnits();
        }
        else if (type == "Bode")
        {
            plot.Axes.SetLimits(0, 1000, -180, 180);
        }
        else if (type is "Waterfall" or "Cascade")
        {
            plot.Axes.SetLimits(0, 1200, 0, 120);
        }
        else
        {
            plot.Axes.SetLimits(0, 1000, 0, 120);
        }
    }

    private static void AddLine(Plot plot, SeriesData series)
    {
        var line = plot.Add.ScatterLine(series.Xs, series.Ys, series.Color);
        line.LegendText = series.Label;
        line.LineWidth = (float)series.Width;
        line.MarkerSize = 0;
    }

    private static SeriesData[] CreateTrendSeries() =>
    [
        Series("RMS trend", ScottPlot.Colors.DodgerBlue,
            GeneratePoints(1200, 0, 1000, i =>
            {
                var x = i / 1199.0;
                return 52 + 22 * Math.Sin(Tau * 3.0 * x) + 8 * Math.Sin(Tau * 17.0 * x + 0.4);
            }))
    ];

    private static SeriesData[] CreateTimeBaseSeries() =>
    [
        Series("X probe", ScottPlot.Colors.DodgerBlue, GeneratePoints(1400, 0, 1000, i =>
        {
            var x = i / 1399.0;
            return 58 + 30 * Math.Sin(Tau * 7 * x) + 7 * Math.Sin(Tau * 41 * x);
        })),
        Series("Y probe", ScottPlot.Colors.MediumSeaGreen, GeneratePoints(1400, 0, 1000, i =>
        {
            var x = i / 1399.0;
            return 52 + 26 * Math.Sin(Tau * 7 * x + 1.2) + 5 * Math.Sin(Tau * 37 * x);
        }))
    ];

    private static SeriesData[] CreateSpectrumSeries() =>
    [
        Series("Spectrum", ScottPlot.Colors.DodgerBlue,
            GeneratePoints(900, 0, 1000, i =>
            {
                var x = i / 899.0;
                return 4 + Peak(x, 0.14, 0.018, 42) + Peak(x, 0.32, 0.022, 96) +
                           Peak(x, 0.52, 0.03, 62) + Peak(x, 0.78, 0.045, 38);
            }))
    ];

    private static SeriesData[] CreateWaterfallSeries(bool cascade)
    {
        var layers = cascade ? 10 : 14;
        var series = new SeriesData[layers];

        for (var layer = 0; layer < layers; layer++)
        {
            var offset = layer * (cascade ? 7.5 : 5.0);
            var xOffset = layer * (cascade ? 16.0 : 9.0);
            var color = new ScottPlotColor((byte)(35 + layer * 10), (byte)(120 + layer * 7), 220, 255);

            series[layer] = Series($"Trace {layer + 1}", color,
                GeneratePoints(260, xOffset, 840 + xOffset, i =>
                {
                    var x = i / 259.0;
                    return offset + 8 + Peak(x, 0.20 + layer * 0.006, 0.025, 26)
                                      + Peak(x, 0.43 - layer * 0.004, 0.035, 38)
                                      + Peak(x, 0.67, 0.055, 20);
                }, 1.2));
        }

        return series;
    }

    private static SeriesData[] CreateOrbitSeries()
    {
        var xs = new double[720];
        var ys = new double[720];

        for (var i = 0; i < xs.Length; i++)
        {
            var t = i / (double)(xs.Length - 1) * Tau;
            xs[i] = Math.Cos(t) * 0.90 + Math.Cos(2.0 * t + 0.3) * 0.10;
            ys[i] = Math.Sin(t + 0.75) * 0.64 + Math.Sin(3.0 * t) * 0.08;
        }

        return [new SeriesData("Orbit", xs, ys, ScottPlot.Colors.DeepSkyBlue, 2.2)];
    }

    private static SeriesData[] CreateOrbitTimeBaseSeries() =>
    [
        Series("Orbit projection", ScottPlot.Colors.DeepSkyBlue,
            GeneratePoints(700, 0, 1000, i =>
            {
                var t = i / 699.0 * Tau * 6.0;
                return 60 + 32 * Math.Sin(t + 0.75) + 5 * Math.Sin(3.0 * t);
            })),
        Series("Time base", ScottPlot.Colors.MediumSeaGreen,
            GeneratePoints(700, 0, 1000, i =>
            {
                var t = i / 699.0 * Tau * 6.0;
                return 48 + 28 * Math.Sin(t) + 6 * Math.Sin(2.0 * t + 0.3);
            }))
    ];

    private static SeriesData[] CreateBodeSeries() =>
    [
        Series("Magnitude", ScottPlot.Colors.DodgerBlue,
            GeneratePoints(800, 0, 1000, i =>
            {
                var x = i / 799.0;
                return -150 + 230 / (1.0 + Math.Exp((x - 0.42) * 12.0)) + Peak(x, 0.58, 0.04, 70);
            })),
        Series("Phase", ScottPlot.Colors.Orange,
            GeneratePoints(800, 0, 1000, i =>
            {
                var x = i / 799.0;
                return 120 - 220 / (1.0 + Math.Exp((x - 0.50) * 9.0)) - 18 * Math.Sin(Tau * x);
            }))
    ];

    private void RenderPolar(Plot plot)
    {
        plot.HideAxesAndGrid();

        var polarAxis = plot.Add.PolarAxis(radius: 20, spokeLength: 20, circleCount: 4, spokeCount: 12);
        polarAxis.FillColor = DataBackground;
        polarAxis.ManageAxisLimits = true;
        polarAxis.SetCircles(20, 4);
        polarAxis.SetSpokes(12, 20, degreeLabels: true);

        var coordinates = new Coordinates[361];
        for (var i = 0; i < coordinates.Length; i++)
        {
            var degrees = i == 360 ? 0 : i;
            var radians = degrees * Math.PI / 180.0;
            var amplitude = 10 + 4 * Math.Cos(radians) + 2 * Math.Sin(degrees * Math.PI / 30.0);
            coordinates[i] = new Coordinates(amplitude * Math.Cos(radians), amplitude * Math.Sin(radians));
        }

        var line = plot.Add.ScatterLine(coordinates, ScottPlot.Colors.DodgerBlue);
        line.LegendText = "Polar";
        line.LineWidth = 2;
        line.MarkerSize = 0;

        plot.Title($"Polar - {EquipmentLabel}");
        plot.Axes.SetLimits(-23, 23, -23, 23);
        plot.Axes.SquareUnits();
    }

    private void RenderSurfaceHeatmap(Plot plot)
    {
        const int sizeX = 48;
        const int sizeZ = 42;
        var data = new double[sizeZ, sizeX];

        for (var z = 0; z < sizeZ; z++)
        {
            for (var x = 0; x < sizeX; x++)
            {
                var nx = x / (double)(sizeX - 1);
                var nz = z / (double)(sizeZ - 1);
                data[z, x] = 30 + 28 * Math.Sin(Tau * nx) * Math.Cos(Tau * nz)
                                + 18 * Peak(nx, 0.62, 0.16, 1) * Peak(nz, 0.42, 0.18, 1);
            }
        }

        var heatmap = plot.Add.Heatmap(data);
        heatmap.Rectangle = new CoordinateRect(0, 100, 0, 100);
        heatmap.FlipRows = true;
        heatmap.Smooth = true;
        heatmap.Colormap = new ScottPlot.Colormaps.Turbo();

        plot.Add.ColorBar(heatmap, Edge.Right);
        ApplyLabels(plot, $"Surface - {EquipmentLabel}", "X", "Z", showLegend: false);
        plot.Axes.SetLimits(0, 100, 0, 100);
    }

    private void RenderCampbellDiagram(Plot plot)
    {
        const double maxRpm = 7000.0;
        const double maxFreq = 1200.0;
        const int orders = 8;

        ScottPlotColor[] palette =
        [
            ScottPlot.Colors.Red, ScottPlot.Colors.OrangeRed, ScottPlot.Colors.Orange, ScottPlot.Colors.Yellow,
            ScottPlot.Colors.LimeGreen, ScottPlot.Colors.Cyan, ScottPlot.Colors.DodgerBlue, ScottPlot.Colors.MediumPurple,
        ];

        for (var n = 1; n <= orders; n++)
        {
            var line = plot.Add.ScatterLine(
                new double[] { 0, maxRpm },
                new double[] { 0, n * maxRpm / 60.0 },
                palette[n - 1]);
            line.LegendText = $"{n}E";
            line.LineWidth = n == 1 ? 2.5f : 1.6f;
            line.MarkerSize = 0;
        }

        ApplyLabels(plot, $"Campbell Diagram - {EquipmentLabel}", "Speed (RPM)", "Frequency (Hz)", showLegend: true);
        plot.Axes.SetLimits(0, maxRpm, 0, maxFreq);
    }

    private void RenderSpectrogram(Plot plot)
    {
        const int freqBins = 200;
        const int timeBins = 30;
        const double maxFreq = 1000.0;

        var data = new double[timeBins, freqBins];
        var rand = new Random(42);
        var p1 = freqBins * 0.14;
        var p2 = freqBins * 0.45;

        for (var row = 0; row < timeBins; row++)
        {
            p1 += (rand.NextDouble() - 0.5) * 1.5;
            p2 += (rand.NextDouble() - 0.5) * 1.0;

            for (var col = 0; col < freqBins; col++)
            {
                var d1 = col - p1;
                var d2 = col - p2;
                var value = 55.0 * Math.Exp(-d1 * d1 / 80.0)
                          + 80.0 * Math.Exp(-d2 * d2 / 40.0)
                          + rand.NextDouble() * 6.0;
                data[row, col] = Math.Min(100.0, value);
            }
        }

        var heatmap = plot.Add.Heatmap(data);
        heatmap.Rectangle = new CoordinateRect(0, maxFreq, 0, timeBins);
        heatmap.FlipRows = true;
        heatmap.Smooth = true;
        heatmap.ManualRange = new ScottPlot.Range(0, 100);
        heatmap.Colormap = new ScottPlot.Colormaps.Turbo();

        plot.Add.ColorBar(heatmap, Edge.Right);
        ApplyLabels(plot, $"Spectrogram - {EquipmentLabel}", "Frequency (Hz)", "Time", showLegend: false);
        plot.Axes.SetLimits(0, maxFreq, 0, timeBins);
    }

    private void RenderWaterfallMap(Plot plot, bool cascade)
    {
        var layers = cascade ? 10 : 14;
        const int sizeX = 160;
        var data = new double[layers, sizeX];

        for (var layer = 0; layer < layers; layer++)
        {
            for (var x = 0; x < sizeX; x++)
            {
                var nx = x / (double)(sizeX - 1);
                data[layer, x] = 8
                    + Peak(nx, 0.20 + layer * 0.006, 0.025, 26)
                    + Peak(nx, 0.43 - layer * 0.004, 0.035, 38)
                    + Peak(nx, 0.67, 0.055, 20);
            }
        }

        var heatmap = plot.Add.Heatmap(data);
        heatmap.Rectangle = new CoordinateRect(0, 840, 0, layers);
        heatmap.FlipRows = true;
        heatmap.Smooth = true;
        heatmap.ManualRange = new ScottPlot.Range(0, 70);
        heatmap.Colormap = new ScottPlot.Colormaps.Turbo();

        plot.Add.ColorBar(heatmap, Edge.Right);
        ApplyLabels(plot, $"{PlotType} 3D - {EquipmentLabel}", "Frequency", "Trace", showLegend: false);
        plot.Axes.SetLimits(0, 840, 0, layers);
    }

    private static SeriesData Series(string label, ScottPlotColor color, (double[] Xs, double[] Ys) points, double width = 1.6) =>
        new(label, points.Xs, points.Ys, color, width);

    private static (double[] Xs, double[] Ys) GeneratePoints(
        int count,
        double xMin,
        double xMax,
        Func<int, double> yFactory,
        double xScale = 1.0)
    {
        var xs = new double[count];
        var ys = new double[count];

        for (var i = 0; i < count; i++)
        {
            xs[i] = (xMin + (xMax - xMin) * i / (count - 1.0)) * xScale;
            ys[i] = yFactory(i);
        }

        return (xs, ys);
    }

    private static double Peak(double x, double center, double width, double height)
    {
        var d = (x - center) / width;
        return height * Math.Exp(-d * d);
    }

    private readonly record struct SeriesData(
        string Label,
        double[] Xs,
        double[] Ys,
        ScottPlotColor Color,
        double Width);

    private const double Tau = Math.PI * 2.0;
}
