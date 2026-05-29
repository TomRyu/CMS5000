using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LightningChartLib.WPF.ChartingMVVM;
using LightningChartLib.WPF.ChartingMVVM.Axes;
using LightningChartLib.WPF.ChartingMVVM.Series3D;
using LightningChartLib.WPF.ChartingMVVM.SeriesPolar;
using LightningChartLib.WPF.ChartingMVVM.SeriesXY;
using LightningChartLib.WPF.ChartingMVVM.Titles;
using LightningChartLib.WPF.ChartingMVVM.Views.View3D;
using LightningChartLib.WPF.ChartingMVVM.Views.ViewPolar;
using LightningChartLib.WPF.ChartingMVVM.Views.ViewXY;

namespace CMS5000.Controls;

public partial class LightningChartSampleView : UserControl
{
    // ── Dependency Properties ─────────────────────────────────────────────

    public static readonly DependencyProperty PlotTypeProperty =
        DependencyProperty.Register(
            nameof(PlotType), typeof(string), typeof(LightningChartSampleView),
            new PropertyMetadata("Spectrum", OnPlotTypeChanged));

    public static readonly DependencyProperty EquipmentNameProperty =
        DependencyProperty.Register(
            nameof(EquipmentName), typeof(string), typeof(LightningChartSampleView),
            new PropertyMetadata("Fan D2", OnEquipmentNameChanged));

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

    // ── State ─────────────────────────────────────────────────────────────

    private bool _is3DMode;
    private bool Supports3D => PlotType is "Waterfall" or "Cascade";

    // ── Construction ──────────────────────────────────────────────────────

    public LightningChartSampleView()
    {
        InitializeComponent();
        Loaded += (_, _) => RenderChart();
    }

    // ── DP Callbacks ──────────────────────────────────────────────────────

    private static void OnPlotTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LightningChartSampleView v || !v.IsLoaded) return;
        v._is3DMode = false;
        v.RenderChart();
    }

    private static void OnEquipmentNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not LightningChartSampleView v || !v.IsLoaded) return;
        v.RenderChart();
    }

    // ── Toggle handlers ───────────────────────────────────────────────────

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

    // ── Render dispatch ───────────────────────────────────────────────────

    private void RenderChart()
    {
        var type = string.IsNullOrWhiteSpace(PlotType) ? "Spectrum" : PlotType;

        XyChart.Visibility      = Visibility.Collapsed;
        PolarChart.Visibility   = Visibility.Collapsed;
        SurfaceChart.Visibility = Visibility.Collapsed;

        UpdateDimToggle();

        if (_is3DMode && type is "Waterfall" or "Cascade")
        {
            RenderWaterfall3D(type == "Cascade");
            SurfaceChart.Visibility = Visibility.Visible;
            return;
        }

        if (type == "Polar")
        {
            RenderPolar();
            PolarChart.Visibility = Visibility.Visible;
            return;
        }

        if (type == "Surface")
        {
            RenderSurface3D();
            SurfaceChart.Visibility = Visibility.Visible;
            return;
        }

        if (type == "Campbell Diagram")
        {
            RenderCampbellDiagram();
            XyChart.Visibility = Visibility.Visible;
            return;
        }

        if (type == "Spectrogram")
        {
            RenderSpectrogram();
            XyChart.Visibility = Visibility.Visible;
            return;
        }

        RenderXY(type);
        XyChart.Visibility = Visibility.Visible;
    }

    // ── 2D/3D toggle UI ──────────────────────────────────────────────────

    private void UpdateDimToggle()
    {
        DimToggle.Visibility = Supports3D ? Visibility.Visible : Visibility.Collapsed;

        var blue    = new SolidColorBrush(Color.FromRgb(0x29, 0x79, 0xFF));
        var dimBg   = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x3E));
        var white   = new SolidColorBrush(Colors.White);
        var dimFg   = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x99));

        Btn2D.Background = _is3DMode ? dimBg  : blue;
        Btn2D.Foreground = _is3DMode ? dimFg  : white;
        Btn3D.Background = _is3DMode ? blue   : dimBg;
        Btn3D.Foreground = _is3DMode ? white  : dimFg;
    }

    // ── XY Charts ─────────────────────────────────────────────────────────

    private void RenderXY(string type)
    {
        var view = CreateViewXY(type);

        view.PointLineSeries = type switch
        {
            "Spectrum"        => CreateSpectrumSeries(),
            "Waterfall"       => CreateWaterfallSeries(false),
            "Cascade"         => CreateWaterfallSeries(true),
            "Orbit"           => CreateOrbitSeries(),
            "Orbit & Time Base" => CreateOrbitTimeBaseSeries(),
            "Time Base"       => CreateTimeBaseSeries(),
            "Bode"            => CreateBodeSeries(),
            _                 => CreateTrendSeries(),
        };

        XyChart.ChartName = $"{type} - {EquipmentName}";
        XyChart.ViewXY    = view;
        XyChart.ViewXY.ZoomToFit();
    }

    private ViewXY CreateViewXY(string type)
    {
        var xAxis = new AxisX
        {
            Title      = new AxisXTitle { Text = type is "Spectrum" or "Waterfall" or "Cascade" or "Bode" ? "Frequency" : "Time" },
            ValueType  = AxisValueType.Number,
            ScrollMode = XAxisScrollMode.None,
        };
        var yAxis = new AxisY
        {
            Title = new AxisYTitle { Text = type == "Orbit" ? "Y probe" : "Amplitude" },
        };

        if (type == "Orbit")
        {
            xAxis.SetRange(-1.4, 1.4);
            yAxis.SetRange(-1.4, 1.4);
        }
        else if (type == "Bode")
        {
            xAxis.SetRange(0, 1000);
            yAxis.SetRange(-180, 180);
        }
        else
        {
            xAxis.SetRange(0, 1000);
            yAxis.SetRange(0, 120);
        }

        var xAxes = new AxisXCollection(); xAxes.Add(xAxis);
        var yAxes = new AxisYCollection(); yAxes.Add(yAxis);

        return new ViewXY { XAxes = xAxes, YAxes = yAxes };
    }

    // ── Series factories ──────────────────────────────────────────────────

    private PointLineSeriesCollection CreateTrendSeries() =>
        Collection(CreateSeries("RMS trend", Colors.DodgerBlue,
            GeneratePoints(1200, 0, 1000, i =>
            {
                var x = i / 1199.0;
                return 52 + 22 * Math.Sin(Tau * 3.0 * x) + 8 * Math.Sin(Tau * 17.0 * x + 0.4);
            })));

    private PointLineSeriesCollection CreateTimeBaseSeries() =>
        Collection(
            CreateSeries("X probe", Colors.DodgerBlue, GeneratePoints(1400, 0, 1000, i =>
            {
                var x = i / 1399.0;
                return 58 + 30 * Math.Sin(Tau * 7 * x) + 7 * Math.Sin(Tau * 41 * x);
            })),
            CreateSeries("Y probe", Colors.MediumSeaGreen, GeneratePoints(1400, 0, 1000, i =>
            {
                var x = i / 1399.0;
                return 52 + 26 * Math.Sin(Tau * 7 * x + 1.2) + 5 * Math.Sin(Tau * 37 * x);
            })));

    private PointLineSeriesCollection CreateSpectrumSeries() =>
        Collection(CreateSeries("Spectrum", Colors.DodgerBlue,
            GeneratePoints(900, 0, 1000, i =>
            {
                var x = i / 899.0;
                return 4 + Peak(x, 0.14, 0.018, 42) + Peak(x, 0.32, 0.022, 96) +
                           Peak(x, 0.52, 0.03, 62)  + Peak(x, 0.78, 0.045, 38);
            })));

    private PointLineSeriesCollection CreateWaterfallSeries(bool cascade)
    {
        var collection = new PointLineSeriesCollection();
        var layers = cascade ? 10 : 14;

        for (var layer = 0; layer < layers; layer++)
        {
            var offset  = layer * (cascade ? 7.5 : 5.0);
            var xOffset = layer * (cascade ? 16.0 : 9.0);
            var color   = Color.FromRgb((byte)(35 + layer * 10), (byte)(120 + layer * 7), 220);
            collection.Add(CreateSeries($"Trace {layer + 1}", color,
                GeneratePoints(260, xOffset, 840 + xOffset, i =>
                {
                    var x = i / 259.0;
                    return offset + 8 + Peak(x, 0.20 + layer * 0.006, 0.025, 26)
                                      + Peak(x, 0.43 - layer * 0.004, 0.035, 38)
                                      + Peak(x, 0.67, 0.055, 20);
                }, 1.2)));
        }

        return collection;
    }

    private PointLineSeriesCollection CreateOrbitSeries()
    {
        var pts = new SeriesPoint[720];
        for (var i = 0; i < pts.Length; i++)
        {
            var t = i / (double)(pts.Length - 1) * Tau;
            pts[i].X = Math.Cos(t) * 0.90 + Math.Cos(2.0 * t + 0.3) * 0.10;
            pts[i].Y = Math.Sin(t + 0.75) * 0.64 + Math.Sin(3.0 * t) * 0.08;
        }
        return Collection(CreateSeries("Orbit", Colors.DeepSkyBlue, pts, 2.2));
    }

    private PointLineSeriesCollection CreateOrbitTimeBaseSeries() =>
        Collection(
            CreateSeries("Orbit projection", Colors.DeepSkyBlue,
                GeneratePoints(700, 0, 1000, i =>
                {
                    var t = i / 699.0 * Tau * 6.0;
                    return 60 + 32 * Math.Sin(t + 0.75) + 5 * Math.Sin(3.0 * t);
                })),
            CreateSeries("Time base", Colors.MediumSeaGreen,
                GeneratePoints(700, 0, 1000, i =>
                {
                    var t = i / 699.0 * Tau * 6.0;
                    return 48 + 28 * Math.Sin(t) + 6 * Math.Sin(2.0 * t + 0.3);
                })));

    private PointLineSeriesCollection CreateBodeSeries() =>
        Collection(
            CreateSeries("Magnitude", Colors.DodgerBlue,
                GeneratePoints(800, 0, 1000, i =>
                {
                    var x = i / 799.0;
                    return -150 + 230 / (1.0 + Math.Exp((x - 0.42) * 12.0)) + Peak(x, 0.58, 0.04, 70);
                })),
            CreateSeries("Phase", Colors.Orange,
                GeneratePoints(800, 0, 1000, i =>
                {
                    var x = i / 799.0;
                    return 120 - 220 / (1.0 + Math.Exp((x - 0.50) * 9.0)) - 18 * Math.Sin(Tau * x);
                })));

    // ── Polar ─────────────────────────────────────────────────────────────

    private void RenderPolar()
    {
        var axis = new AxisPolar
        {
            InnerCircleRadiusPercentage = 8,
            MinAmplitude   = 0,
            MaxAmplitude   = 20,
            MajorDivCount  = 4,
            AllowScaling   = true,
            AllowScrolling = true,
        };
        var axes = new AxisPolarCollection(); axes.Add(axis);

        var pts = new PolarSeriesPoint[360];
        for (var i = 0; i < pts.Length; i++)
        {
            pts[i].Angle     = i;
            pts[i].Amplitude = 10 + 4 * Math.Cos(i * Math.PI / 180.0) + 2 * Math.Sin(i * Math.PI / 30.0);
        }

        var series = new PointLineSeriesPolar { Points = pts, PointsVisible = true };
        series.LineStyle.Color = Colors.DodgerBlue;
        series.LineStyle.Width = 2;

        var sc = new PointLineSeriesPolarCollection(); sc.Add(series);

        PolarChart.ChartName  = $"Polar - {EquipmentName}";
        PolarChart.ViewPolar  = new ViewPolar { Axes = axes, PointLineSeries = sc };
    }

    // ── Surface 3D (Surface 노드 전용) ────────────────────────────────────

    private void RenderSurface3D()
    {
        var surface = new SurfaceGridSeries3D
        {
            RangeMinX = 0, RangeMaxX = 100,
            RangeMinZ = 0, RangeMaxZ = 100,
            SizeX = 48, SizeZ = 42,
        };
        surface.ContourPalette = new ValueRangePalette(surface);

        for (var x = 0; x < surface.SizeX; x++)
            for (var z = 0; z < surface.SizeZ; z++)
            {
                var nx = x / (double)(surface.SizeX - 1);
                var nz = z / (double)(surface.SizeZ - 1);
                surface.Data[x, z].Y = (float)(30 + 28 * Math.Sin(Tau * nx) * Math.Cos(Tau * nz)
                                                   + 18 * Peak(nx, 0.62, 0.16, 1) * Peak(nz, 0.42, 0.18, 1));
            }

        var surfaces = new SurfaceGridSeries3DCollection(); surfaces.Add(surface);

        SurfaceChart.ChartName = $"Surface - {EquipmentName}";
        SurfaceChart.View3D    = new View3D
        {
            SurfaceGridSeries3D = surfaces,
            Lights = View3D.CreateDefaultLights(),
            Camera = new Camera3D { RotationX = 38, RotationY = 0, RotationZ = 32 },
        };
    }

    // ── Campbell Diagram ─────────────────────────────────────────────────
    // X: RPM(속도), Y: Hz(주파수), 대각선=차수 조화선(1E~8E)

    private void RenderCampbellDiagram()
    {
        const double maxRpm  = 7000.0;
        const double maxFreq = 1200.0;
        const int    orders  = 8;

        var xAxis = new AxisX
        {
            Title      = new AxisXTitle { Text = "Speed (RPM)" },
            ValueType  = AxisValueType.Number,
            ScrollMode = XAxisScrollMode.None,
        };
        xAxis.SetRange(0, maxRpm);

        var yAxis = new AxisY { Title = new AxisYTitle { Text = "Frequency (Hz)" } };
        yAxis.SetRange(0, maxFreq);

        var xAxes = new AxisXCollection(); xAxes.Add(xAxis);
        var yAxes = new AxisYCollection(); yAxes.Add(yAxis);

        Color[] palette =
        [
            Colors.Red, Colors.OrangeRed, Colors.Orange, Colors.Yellow,
            Colors.LimeGreen, Colors.Cyan, Colors.DodgerBlue, Colors.MediumPurple,
        ];

        var collection = new PointLineSeriesCollection();

        for (int n = 1; n <= orders; n++)
        {
            var s = new PointLineSeries { PointsVisible = false, ShowInLegendBox = true };
            s.Title.Text        = $"{n}E";
            s.LineStyle.Color   = palette[n - 1];
            s.LineStyle.Width   = n == 1 ? 2.5 : 1.6;

            var pts = new SeriesPoint[2];
            pts[0].X = 0;    pts[0].Y = 0;
            pts[1].X = maxRpm; pts[1].Y = n * maxRpm / 60.0;
            s.Points = pts;
            s.InvalidateData();
            collection.Add(s);
        }

        var view = new ViewXY { XAxes = xAxes, YAxes = yAxes, PointLineSeries = collection };
        XyChart.ChartName = $"Campbell Diagram - {EquipmentName}";
        XyChart.ViewXY    = view;
        XyChart.ViewXY.ZoomToFit();
    }

    // ── Spectrogram ───────────────────────────────────────────────────────
    // X: 주파수(Hz), Y: 시간(측정 인덱스), 색상=진폭

    private void RenderSpectrogram()
    {
        const int    freqBins = 200;
        const int    timeBins = 30;
        const double maxFreq  = 1000.0;

        var xAxis = new AxisX
        {
            Title      = new AxisXTitle { Text = "Frequency (Hz)" },
            ValueType  = AxisValueType.Number,
            ScrollMode = XAxisScrollMode.None,
        };
        xAxis.SetRange(0, maxFreq);

        var yAxis = new AxisY { Title = new AxisYTitle { Text = "Time" } };
        yAxis.SetRange(0, timeBins);

        var xAxes = new AxisXCollection(); xAxes.Add(xAxis);
        var yAxes = new AxisYCollection(); yAxes.Add(yAxis);

        // 정적 데모 데이터: 두 주파수 피크가 시간에 따라 미세 이동
        var data   = new double[timeBins][];
        var rand   = new Random(42);
        double p1  = freqBins * 0.14;
        double p2  = freqBins * 0.45;

        for (int row = 0; row < timeBins; row++)
        {
            data[row] = new double[freqBins];
            p1 += (rand.NextDouble() - 0.5) * 1.5;
            p2 += (rand.NextDouble() - 0.5) * 1.0;

            for (int col = 0; col < freqBins; col++)
            {
                double d1 = col - p1, d2 = col - p2;
                double v  = 55.0 * Math.Exp(-d1 * d1 / 80.0)
                          + 80.0 * Math.Exp(-d2 * d2 / 40.0)
                          + rand.NextDouble() * 6.0;
                data[row][col] = Math.Min(100.0, v);
            }
        }

        var grid = new IntensityGridSeries
        {
            ContourLineType      = ContourLineTypeXY.None,
            WireframeType        = SurfaceWireframeType.None,
            PixelRendering       = true,
            AllowUserInteraction = false,
        };
        grid.Title.Text = "P(f, t)";
        grid.SetValuesData(data, IntensityGridValuesDataOrder.RowsColumns);
        grid.SetRangesXY(0, maxFreq, 0, timeBins);

        var pal = new ValueRangePalette(grid);
        pal.Steps.Clear();
        pal.Steps.Add(new PaletteStep(pal, Color.FromRgb(0x05, 0x00, 0x20), 0));
        pal.Steps.Add(new PaletteStep(pal, Colors.DarkBlue,    15));
        pal.Steps.Add(new PaletteStep(pal, Colors.DodgerBlue,  35));
        pal.Steps.Add(new PaletteStep(pal, Colors.Yellow,      65));
        pal.Steps.Add(new PaletteStep(pal, Colors.Red,        100));
        pal.Type = PaletteType.Gradient;
        grid.ValueRangePalette = pal;

        var gridCol = new IntensityGridSeriesCollection();
        gridCol.Add(grid);

        var view = new ViewXY
        {
            XAxes               = xAxes,
            YAxes               = yAxes,
            IntensityGridSeries = gridCol,
        };

        XyChart.ChartName = $"Spectrogram - {EquipmentName}";
        XyChart.ViewXY    = view;
    }

    // ── Waterfall / Cascade → 3D surface ─────────────────────────────────

    private void RenderWaterfall3D(bool cascade)
    {
        var layers = cascade ? 10 : 14;
        const int sizeX = 80;

        var surface = new SurfaceGridSeries3D
        {
            RangeMinX = 0,
            RangeMaxX = 840,
            RangeMinZ = 0,
            RangeMaxZ = layers - 1.0,
            SizeX = sizeX,
            SizeZ = layers,
        };
        surface.ContourPalette = new ValueRangePalette(surface);

        for (var z = 0; z < layers; z++)
            for (var x = 0; x < sizeX; x++)
            {
                var nx = x / (double)(sizeX - 1);
                surface.Data[x, z].Y = (float)(8
                    + Peak(nx, 0.20 + z * 0.006, 0.025, 26)
                    + Peak(nx, 0.43 - z * 0.004, 0.035, 38)
                    + Peak(nx, 0.67, 0.055, 20));
            }

        var surfaces = new SurfaceGridSeries3DCollection(); surfaces.Add(surface);

        SurfaceChart.ChartName = $"{PlotType} 3D - {EquipmentName}";
        SurfaceChart.View3D    = new View3D
        {
            SurfaceGridSeries3D = surfaces,
            Lights = View3D.CreateDefaultLights(),
            Camera = new Camera3D { RotationX = 38, RotationY = 0, RotationZ = 32 },
        };
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static PointLineSeries CreateSeries(string title, Color color, SeriesPoint[] pts, double width = 1.6)
    {
        var s = new PointLineSeries
        {
            Points        = pts,
            PointsVisible = false,
            Title         = new SeriesTitle { Text = title },
            ShowInLegendBox = true,
        };
        s.LineStyle.Color = color;
        s.LineStyle.Width = width;
        return s;
    }

    private static PointLineSeriesCollection Collection(params PointLineSeries[] series)
    {
        var col = new PointLineSeriesCollection();
        foreach (var s in series) col.Add(s);
        return col;
    }

    private static SeriesPoint[] GeneratePoints(int count, double xMin, double xMax,
        Func<int, double> yFactory, double xScale = 1.0)
    {
        var pts = new SeriesPoint[count];
        for (var i = 0; i < count; i++)
        {
            pts[i].X = (xMin + (xMax - xMin) * i / (count - 1.0)) * xScale;
            pts[i].Y = yFactory(i);
        }
        return pts;
    }

    private static double Peak(double x, double center, double width, double height)
    {
        var d = (x - center) / width;
        return height * Math.Exp(-d * d);
    }

    private const double Tau = Math.PI * 2.0;
}
