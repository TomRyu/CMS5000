using System.Windows;
using System.Windows.Controls;
using CMS5000.Models.Monitoring;
using SpColor = ScottPlot.Color;

namespace CMS5000.Controls;

/// <summary>Status &gt; Bar Graph 카드용 ScottPlot 단일 막대 차트.</summary>
public partial class MiniBarPlot : UserControl
{
    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(MiniBarPlot),
            new PropertyMetadata(0.0, OnChanged));

    public static readonly DependencyProperty AxisMaxProperty =
        DependencyProperty.Register(nameof(AxisMax), typeof(double), typeof(MiniBarPlot),
            new PropertyMetadata(1.0, OnChanged));

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(MonStatus), typeof(MiniBarPlot),
            new PropertyMetadata(MonStatus.Good, OnChanged));

    public double    Value   { get => (double)GetValue(ValueProperty);   set => SetValue(ValueProperty, value); }
    public double    AxisMax { get => (double)GetValue(AxisMaxProperty); set => SetValue(AxisMaxProperty, value); }
    public MonStatus Status  { get => (MonStatus)GetValue(StatusProperty); set => SetValue(StatusProperty, value); }

    public MiniBarPlot()
    {
        InitializeComponent();
        Loaded += (_, _) => Render();
    }

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MiniBarPlot v && v.IsLoaded) v.Render();
    }

    private static SpColor Hex(MonStatus s) => s switch
    {
        MonStatus.Good    => SpColor.FromHex("#22C55E"),
        MonStatus.Warning => SpColor.FromHex("#EAB308"),
        MonStatus.Alert   => SpColor.FromHex("#F97316"),
        MonStatus.Alarm   => SpColor.FromHex("#EF4444"),
        _                 => SpColor.FromHex("#94A3B8"),
    };

    private void Render()
    {
        try
        {
            var plot = PlotView.Plot;
            plot.Clear();

            var bar = new ScottPlot.Bar
            {
                Position  = 0,
                Value     = Value,
                FillColor = Hex(Status),
                LineWidth = 0,
                Size      = 0.9,
            };
            plot.Add.Bar(bar);

            // 카드 스타일: 흰 배경 + 옅은 점선 그리드 + 좌측 눈금만
            plot.FigureBackground.Color = SpColor.FromHex("#FFFFFF");
            plot.DataBackground.Color   = SpColor.FromHex("#FFFFFF");
            plot.Axes.Color(SpColor.FromHex("#94A3B8"));
            plot.Grid.MajorLineColor = SpColor.FromHex("#E2E8F0");
            plot.Grid.MajorLineWidth = 1;

            plot.Axes.Bottom.IsVisible = false;          // x축 숨김
            plot.Axes.SetLimits(-0.7, 0.7, 0, AxisMax <= 0 ? 1 : AxisMax);

            PlotView.Refresh();
        }
        catch { /* 렌더 실패는 무시(빈 카드) */ }
    }
}
