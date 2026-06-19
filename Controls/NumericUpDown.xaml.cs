using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace CMS5000.Controls;

/// <summary>
/// 원본(WinForms NumericUpDown) 재현용 숫자 입력 컨트롤. 텍스트 + 상/하 스피너.
/// <see cref="DecimalPlaces"/> 로 소수 자릿수를 지정한다(기본 0 = 정수).
/// </summary>
public partial class NumericUpDown : UserControl
{
    private bool _updating;

    public NumericUpDown()
    {
        InitializeComponent();
        UpdateText();
    }

    public static readonly DependencyProperty ValueProperty =
        DependencyProperty.Register(nameof(Value), typeof(double), typeof(NumericUpDown),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnValueChanged));

    public double Value
    {
        get => (double)GetValue(ValueProperty);
        set => SetValue(ValueProperty, value);
    }

    public static readonly DependencyProperty MinimumProperty =
        DependencyProperty.Register(nameof(Minimum), typeof(double), typeof(NumericUpDown), new PropertyMetadata(0.0));

    public double Minimum
    {
        get => (double)GetValue(MinimumProperty);
        set => SetValue(MinimumProperty, value);
    }

    public static readonly DependencyProperty MaximumProperty =
        DependencyProperty.Register(nameof(Maximum), typeof(double), typeof(NumericUpDown), new PropertyMetadata((double)int.MaxValue));

    public double Maximum
    {
        get => (double)GetValue(MaximumProperty);
        set => SetValue(MaximumProperty, value);
    }

    public static readonly DependencyProperty DecimalPlacesProperty =
        DependencyProperty.Register(nameof(DecimalPlaces), typeof(int), typeof(NumericUpDown),
            new PropertyMetadata(0, (d, _) => ((NumericUpDown)d).UpdateText()));

    public int DecimalPlaces
    {
        get => (int)GetValue(DecimalPlacesProperty);
        set => SetValue(DecimalPlacesProperty, value);
    }

    public static readonly DependencyProperty IsReadOnlyProperty =
        DependencyProperty.Register(nameof(IsReadOnly), typeof(bool), typeof(NumericUpDown), new PropertyMetadata(false));

    /// <summary>읽기 전용: 텍스트 편집 불가 + 스피너 비활성(회색 처리는 하지 않음).</summary>
    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    private double Step => Math.Pow(10, -DecimalPlaces);

    private static void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var c = (NumericUpDown)d;
        double v = (double)e.NewValue;
        if (v < c.Minimum) { c.Value = c.Minimum; return; }
        if (v > c.Maximum) { c.Value = c.Maximum; return; }
        c.UpdateText();
    }

    private void UpdateText()
    {
        if (PART_Text == null) return;
        _updating = true;
        PART_Text.Text = Value.ToString("F" + DecimalPlaces, CultureInfo.InvariantCulture);
        _updating = false;
    }

    private void Commit()
    {
        if (_updating || PART_Text == null) return;
        if (double.TryParse(PART_Text.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double v))
            Value = Math.Clamp(v, Minimum, Maximum);
        else
            UpdateText();
    }

    private void Text_LostFocus(object sender, RoutedEventArgs e) => Commit();

    private void Text_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) Commit();
    }

    private void Up_Click(object sender, RoutedEventArgs e)   => Value = Math.Min(Maximum, Value + Step);
    private void Down_Click(object sender, RoutedEventArgs e) => Value = Math.Max(Minimum, Value - Step);
}
