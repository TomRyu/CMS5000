using System.Windows;
using System.Windows.Controls;
using ScottPlot;
using SpColor = ScottPlot.Color;

namespace CMS5000.Controls;

/// <summary>
/// Plots 탭용 라이트 테마 ScottPlot 차트. PlotType 문자열로 12종 플롯을 렌더(샘플/합성 데이터).
/// </summary>
public partial class MonitoringPlotView : UserControl
{
    public static readonly DependencyProperty PlotTypeProperty =
        DependencyProperty.Register(nameof(PlotType), typeof(string), typeof(MonitoringPlotView),
            new PropertyMetadata("Trend Plot", OnChanged));

    public string PlotType { get => (string)GetValue(PlotTypeProperty); set => SetValue(PlotTypeProperty, value); }

    private static readonly SpColor Fig  = SpColor.FromHex("#FFFFFF");
    private static readonly SpColor Axis = SpColor.FromHex("#475569");
    private static readonly SpColor Grid = SpColor.FromHex("#E2E8F0");
    private const double Tau = Math.PI * 2.0;

    public MonitoringPlotView()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MonitoringPlotView v && v.IsLoaded) v.Render();
    }

    private void Render()
    {
        try
        {
            var plot = PlotView.Plot;
            plot.Clear();
            plot.ShowAxesAndGrid();
            plot.Legend.IsVisible = false;
            plot.FigureBackground.Color = Fig;
            plot.DataBackground.Color = Fig;
            plot.Axes.Color(Axis);
            plot.Grid.MajorLineColor = Grid;
            plot.Grid.MajorLineWidth = 1;

            switch (PlotType)
            {
                case "Stacked Trend":     RenderStacked(plot); break;
                case "Bode Plot":         RenderBode(plot); break;
                case "Polar Plot":        RenderPolar(plot); break;
                case "Shaft Centerline":  RenderScatter(plot, true); break;
                case "XvsY Plot":         RenderScatter(plot, false); break;
                case "Spectrum (FFT)":
                case "Spectrum + Trend":  RenderSpectrum(plot); break;
                case "Waterfall Plot":    RenderWaterfall(plot, false); break;
                case "Cascade Plot":      RenderWaterfall(plot, true); break;
                case "Timebase":          RenderTimebase(plot); break;
                case "Multi-point Orbit": RenderOrbit(plot); break;
                case "Polar":             RenderPolar(plot); break;
                default:                  RenderTrend(plot); break;   // Trend Plot
            }

            PlotView.Refresh();
        }
        catch { /* 렌더 실패 무시 */ }
    }

    private void Line(Plot plot, double[] xs, double[] ys, SpColor color, string label, float width = 1.8f)
    {
        var l = plot.Add.ScatterLine(xs, ys, color);
        l.LineWidth = width; l.MarkerSize = 0; l.LegendText = label;
    }

    private static (double[] xs, double[] ys) Gen(int n, double xMin, double xMax, Func<double, double> f)
    {
        var xs = new double[n]; var ys = new double[n];
        for (int i = 0; i < n; i++) { var x = xMin + (xMax - xMin) * i / (n - 1.0); xs[i] = x; ys[i] = f(i / (n - 1.0)); }
        return (xs, ys);
    }

    private static double Peak(double x, double c, double w, double h) { var d = (x - c) / w; return h * Math.Exp(-d * d); }

    private void RenderTrend(Plot plot)
    {
        var (x1, y1) = Gen(300, 0, 50, t => 1.4 + 0.5 * Math.Sin(Tau * 3 * t) + 0.15 * Math.Sin(Tau * 11 * t));
        var (x2, y2) = Gen(300, 0, 50, t => 3.0 + 0.6 * Math.Sin(Tau * 2 * t + 0.6));
        Line(plot, x1, y1, ScottPlot.Colors.DodgerBlue, "Direct (mm/s)");
        Line(plot, x2, y2, ScottPlot.Colors.Red, "Speed (RPM)");
        Finish(plot, "Time", "Amplitude");
    }

    private void RenderStacked(Plot plot)
    {
        for (int k = 0; k < 6; k++)
        {
            var off = k * 2.5;
            var (xs, ys) = Gen(300, 0, 50, t => off + 1.0 + 0.6 * Math.Sin(Tau * (2 + k) * t + k));
            Line(plot, xs, ys, new SpColor((byte)(40 + k * 30), (byte)(110 + k * 20), 220, 255), $"Var {k + 1}", 1.4f);
        }
        Finish(plot, "Time", "Stacked");
    }

    private void RenderBode(Plot plot)
    {
        var (xm, ym) = Gen(400, 0, 5000, t => 0.5 + 4.5 / (1 + Math.Exp((t - 0.45) * 14)) + Peak(t, 0.5, 0.04, 3));
        var (xp, yp) = Gen(400, 0, 5000, t => 2.6 - 1.8 / (1 + Math.Exp((t - 0.5) * 10)));
        Line(plot, xm, ym, ScottPlot.Colors.DodgerBlue, "Magnitude");
        Line(plot, xp, yp, ScottPlot.Colors.Orange, "Phase");
        Finish(plot, "Speed (RPM)", "Magnitude / Phase");
    }

    private void RenderPolar(Plot plot)
    {
        var xs = new double[720]; var ys = new double[720];
        for (int i = 0; i < xs.Length; i++)
        {
            var t = i / (double)(xs.Length - 1) * Tau;
            xs[i] = Math.Cos(t) * 0.9 + Math.Cos(2 * t + 0.3) * 0.12;
            ys[i] = Math.Sin(t + 0.7) * 0.66 + Math.Sin(3 * t) * 0.08;
        }
        var l = plot.Add.ScatterLine(xs, ys, ScottPlot.Colors.DodgerBlue);
        l.LineWidth = 2.2f; l.MarkerSize = 0;
        plot.Axes.SetLimits(-1.3, 1.3, -1.3, 1.3);
        plot.Axes.SquareUnits();
        plot.Title("Polar / Orbit");
        plot.XLabel("X probe"); plot.YLabel("Y probe");
    }

    private void RenderOrbit(Plot plot) => RenderPolar(plot);

    private void RenderScatter(Plot plot, bool centerline)
    {
        var rand = new Random(centerline ? 7 : 3);
        int n = centerline ? 400 : 600;
        var xs = new double[n]; var ys = new double[n];
        for (int i = 0; i < n; i++)
        {
            if (centerline) { xs[i] = -10 + rand.NextDouble() * 2 - 1; ys[i] = -5 + rand.NextDouble() * 2 - 1; }
            else { var step = i / (double)n * 4 - 2; xs[i] = step + (rand.NextDouble() - 0.5) * 0.6; ys[i] = Math.Round(step) + (rand.NextDouble() - 0.5) * 0.8; }
        }
        var sc = plot.Add.ScatterPoints(xs, ys, ScottPlot.Colors.DodgerBlue);
        sc.MarkerSize = 5;
        Finish(plot, centerline ? "Gap (X)" : "Power Output", centerline ? "Gap (Y)" : "Temperature");
    }

    private void RenderSpectrum(Plot plot)
    {
        var (xs, ys) = Gen(900, 0, 200, t => 0.2 + Peak(t, 0.30, 0.012, 1.9) + Peak(t, 0.60, 0.012, 0.9) + Peak(t, 0.90, 0.012, 0.45) + Peak(t, 0.15, 0.03, 0.3));
        Line(plot, xs, ys, ScottPlot.Colors.DodgerBlue, "Spectrum", 1.6f);
        // 1X / 2X / 3X 마커
        foreach (var (fx, col) in new[] { (60.0, ScottPlot.Colors.Red), (120.0, ScottPlot.Colors.DodgerBlue), (180.0, ScottPlot.Colors.Green) })
        {
            var vl = plot.Add.VerticalLine(fx, 1.5f, col);
            vl.LinePattern = LinePattern.Dashed;
        }
        Finish(plot, "Frequency (Hz)", "Amplitude (mm/s)");
    }

    private void RenderWaterfall(Plot plot, bool cascade)
    {
        int layers = cascade ? 12 : 16;
        for (int k = 0; k < layers; k++)
        {
            var off = k * (cascade ? 2.2 : 1.6);
            var (xs, ys) = Gen(220, 0, 200, t => off + 0.4 + Peak(t, 0.25 + k * 0.005, 0.02, 2.4) + Peak(t, 0.5 - k * 0.004, 0.025, 1.6) + Peak(t, 0.78, 0.03, 1.0));
            Line(plot, xs, ys, new SpColor((byte)(60 + k * 9), (byte)(90 + k * 8), 230, 255), $"T{k}", 1.1f);
        }
        Finish(plot, "Frequency (Hz)", cascade ? "RPM step" : "Trace");
    }

    private void RenderTimebase(Plot plot)
    {
        var (xs, ys) = Gen(1000, 0, 100, t => 2.2 * Math.Sin(Tau * 5 * t) + 0.4 * Math.Sin(Tau * 23 * t));
        Line(plot, xs, ys, ScottPlot.Colors.DodgerBlue, "Waveform", 1.6f);
        Finish(plot, "Time (ms)", "Amplitude (units)");
    }

    private void Finish(Plot plot, string x, string y)
    {
        plot.XLabel(x);
        plot.YLabel(y);
        plot.Axes.AutoScale();
    }
}
