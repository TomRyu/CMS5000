using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace CMS5000.Controls;

public partial class LightningChartSampleView : UserControl
{
    public static readonly DependencyProperty PlotTypeProperty =
        DependencyProperty.Register(nameof(PlotType), typeof(string), typeof(LightningChartSampleView),
            new PropertyMetadata("Spectrum", OnPlotTypeChanged));

    public static readonly DependencyProperty EquipmentNameProperty =
        DependencyProperty.Register(nameof(EquipmentName), typeof(string), typeof(LightningChartSampleView),
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

    public LightningChartSampleView()
    {
        InitializeComponent();
        SizeChanged += (_, _) => Render();
        Loaded    += (_, _) => Render();
    }

    private static void OnPlotTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LightningChartSampleView v && v.IsLoaded) v.Render();
    }

    private static void OnEquipmentNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is LightningChartSampleView v && v.IsLoaded) v.Render();
    }

    private void Render()
    {
        var w = ChartCanvas.ActualWidth;
        var h = ChartCanvas.ActualHeight;
        if (w < 10 || h < 10) return;

        ChartCanvas.Children.Clear();

        DrawGrid(w, h);

        var type = PlotType ?? "Spectrum";
        ChartTitle.Text = $"{type}  —  {EquipmentName}";

        switch (type)
        {
            case "Waterfall":
            case "Cascade":
                DrawWaterfall(w, h, type == "Cascade");
                break;
            case "Orbit":
                DrawOrbit(w, h);
                XAxisLabel.Text = "X probe";
                YAxisLabel.Text = "Y probe";
                return;
            case "Polar":
                DrawPolar(w, h);
                XAxisLabel.Text = "";
                YAxisLabel.Text = "";
                return;
            case "Bode":
                DrawBode(w, h);
                XAxisLabel.Text = "Frequency (Hz)";
                YAxisLabel.Text = "Magnitude / Phase";
                return;
            default:
                DrawLine(w, h, type);
                break;
        }

        XAxisLabel.Text = type is "Spectrum" or "Waterfall" or "Cascade" ? "Frequency (Hz)" : "Time";
        YAxisLabel.Text = "Amplitude";
    }

    // ── 배경 그리드 ───────────────────────────────────────────────────────────

    private void DrawGrid(double w, double h)
    {
        var gridBrush = new SolidColorBrush(Color.FromArgb(0x22, 0x88, 0x88, 0xAA));
        for (var i = 1; i < 5; i++)
        {
            ChartCanvas.Children.Add(new Line
            {
                X1 = 0, X2 = w,
                Y1 = h * i / 5, Y2 = h * i / 5,
                Stroke = gridBrush, StrokeThickness = 1
            });
            ChartCanvas.Children.Add(new Line
            {
                X1 = w * i / 5, X2 = w * i / 5,
                Y1 = 0, Y2 = h,
                Stroke = gridBrush, StrokeThickness = 1
            });
        }
    }

    // ── 일반 선 차트 (Spectrum / Trend / Time Base 등) ────────────────────────

    private void DrawLine(double w, double h, string type)
    {
        var points = GeneratePoints(w, h, type);
        var pl = new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(Color.FromRgb(0x29, 0x79, 0xFF)),
            StrokeThickness = 1.8,
            StrokeLineJoin = PenLineJoin.Round,
        };
        // 면 채우기
        var fill = new Polygon
        {
            Points = new PointCollection(points) { new(w, h), new(0, h) },
            Fill   = new LinearGradientBrush(
                Color.FromArgb(0x40, 0x29, 0x79, 0xFF),
                Color.FromArgb(0x00, 0x29, 0x79, 0xFF), 90),
        };
        ChartCanvas.Children.Add(fill);
        ChartCanvas.Children.Add(pl);
    }

    // ── Waterfall / Cascade ───────────────────────────────────────────────────

    private void DrawWaterfall(double w, double h, bool cascade)
    {
        var layers = cascade ? 8 : 12;
        for (var layer = layers - 1; layer >= 0; layer--)
        {
            var t = layer / (double)(layers - 1);
            var yOffset = h * 0.55 * t;
            var r = (byte)(30  + (int)(t * 120));
            var g = (byte)(100 + (int)(t * 80));
            var b = (byte)220;
            var pts = GenerateWaterfallLayer(w, h, layer, layers, yOffset);
            ChartCanvas.Children.Add(new Polyline
            {
                Points = pts,
                Stroke = new SolidColorBrush(Color.FromArgb(0xCC, r, g, b)),
                StrokeThickness = 1.4,
            });
        }
    }

    private static PointCollection GenerateWaterfallLayer(double w, double h, int layer, int layers, double yOffset)
    {
        const int n = 120;
        var pts = new PointCollection(n);
        for (var i = 0; i < n; i++)
        {
            var x = w * i / (n - 1.0);
            var nx = i / (n - 1.0);
            var y = h * 0.75 - yOffset
                  - h * 0.22 * Peak(nx, 0.22 + layer * 0.006, 0.03)
                  - h * 0.32 * Peak(nx, 0.45 - layer * 0.004, 0.04)
                  - h * 0.14 * Peak(nx, 0.68, 0.05);
            pts.Add(new Point(x, y));
        }
        return pts;
    }

    // ── Orbit ─────────────────────────────────────────────────────────────────

    private void DrawOrbit(double w, double h)
    {
        const int n = 720;
        var cx = w / 2; var cy = h / 2;
        var rx = w * 0.32; var ry = h * 0.28;
        var pts = new PointCollection(n);
        for (var i = 0; i < n; i++)
        {
            var t = i / (double)(n - 1) * Math.PI * 2;
            pts.Add(new Point(
                cx + rx * (Math.Cos(t) * 0.9 + Math.Cos(2 * t + 0.3) * 0.1),
                cy + ry * (Math.Sin(t + 0.75) * 0.64 + Math.Sin(3 * t) * 0.08)));
        }
        ChartCanvas.Children.Add(new Polyline
        {
            Points = pts,
            Stroke = new SolidColorBrush(Color.FromRgb(0x00, 0xCC, 0xFF)),
            StrokeThickness = 2,
        });
        // 중심 십자선
        var cross = new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF));
        ChartCanvas.Children.Add(new Line { X1 = cx, X2 = cx, Y1 = 0, Y2 = h, Stroke = cross, StrokeThickness = 1 });
        ChartCanvas.Children.Add(new Line { X1 = 0, X2 = w, Y1 = cy, Y2 = cy, Stroke = cross, StrokeThickness = 1 });
    }

    // ── Polar ─────────────────────────────────────────────────────────────────

    private void DrawPolar(double w, double h)
    {
        var cx = w / 2; var cy = h / 2;
        var maxR = Math.Min(w, h) * 0.40;
        var gridBrush = new SolidColorBrush(Color.FromArgb(0x33, 0x88, 0x88, 0xAA));
        for (var ring = 1; ring <= 4; ring++)
        {
            var r = maxR * ring / 4;
            ChartCanvas.Children.Add(new Ellipse
            {
                Width = r * 2, Height = r * 2,
                Stroke = gridBrush, StrokeThickness = 1,
                Margin = new Thickness(cx - r, cy - r, 0, 0),
            });
        }
        const int n = 360;
        var pts = new PointCollection(n + 1);
        for (var i = 0; i <= n; i++)
        {
            var angle = i * Math.PI / 180;
            var amp = maxR * (0.55 + 0.22 * Math.Cos(angle) + 0.12 * Math.Sin(angle * 7));
            pts.Add(new Point(cx + amp * Math.Cos(angle), cy + amp * Math.Sin(angle)));
        }
        ChartCanvas.Children.Add(new Polyline
        {
            Points = pts,
            Stroke = new SolidColorBrush(Color.FromRgb(0x29, 0x79, 0xFF)),
            StrokeThickness = 1.8,
        });
    }

    // ── Bode ─────────────────────────────────────────────────────────────────

    private void DrawBode(double w, double h)
    {
        var mid = h / 2;
        var ptsM = new PointCollection();
        var ptsP = new PointCollection();
        for (var i = 0; i < 200; i++)
        {
            var x  = w * i / 199.0;
            var nx = i / 199.0;
            ptsM.Add(new Point(x, mid - h * 0.35 * (1 - 1 / (1 + Math.Exp((nx - 0.42) * 12))) - h * 0.10 * Peak(nx, 0.58, 0.04)));
            ptsP.Add(new Point(x, mid + h * 0.35 * (1 / (1 + Math.Exp((nx - 0.50) * 9)))));
        }
        ChartCanvas.Children.Add(new Polyline { Points = ptsM, Stroke = new SolidColorBrush(Color.FromRgb(0x29, 0x79, 0xFF)), StrokeThickness = 1.8 });
        ChartCanvas.Children.Add(new Polyline { Points = ptsP, Stroke = new SolidColorBrush(Color.FromRgb(0xFF, 0x99, 0x22)), StrokeThickness = 1.8 });
        ChartCanvas.Children.Add(new Line { X1 = 0, X2 = w, Y1 = mid, Y2 = mid, Stroke = new SolidColorBrush(Color.FromArgb(0x44, 0xFF, 0xFF, 0xFF)), StrokeThickness = 1, StrokeDashArray = new DoubleCollection { 4, 3 } });
    }

    // ── 포인트 생성 ───────────────────────────────────────────────────────────

    private static PointCollection GeneratePoints(double w, double h, string type)
    {
        const int n = 200;
        var pts = new PointCollection(n);
        for (var i = 0; i < n; i++)
        {
            var x  = w * i / (n - 1.0);
            var nx = i / (n - 1.0);
            double y = type switch
            {
                "Spectrum"   => h * 0.85 - h * (0.04 + 0.42 * Peak(nx, 0.32, 0.022) + 0.28 * Peak(nx, 0.14, 0.018) + 0.18 * Peak(nx, 0.52, 0.03) + 0.10 * Peak(nx, 0.78, 0.045)),
                "Spectrogram"=> h * (0.3 + 0.4 * Math.Abs(Math.Sin(nx * Math.PI * 4))),
                _            => h * (0.45 + 0.25 * Math.Sin(nx * Math.PI * 6) + 0.06 * Math.Sin(nx * Math.PI * 41)),
            };
            pts.Add(new Point(x, y));
        }
        return pts;
    }

    private static double Peak(double x, double center, double width)
    {
        var d = (x - center) / width;
        return Math.Exp(-d * d);
    }
}
