using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace CMS5000.Controls;

public class MdiChild : Control
{
    static MdiChild() => DefaultStyleKeyProperty.OverrideMetadata(typeof(MdiChild), new FrameworkPropertyMetadata(typeof(MdiChild)));

    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(MdiChild), new PropertyMetadata("Window"));

    public static readonly DependencyProperty ContentProperty =
        DependencyProperty.Register(nameof(Content), typeof(UIElement), typeof(MdiChild));

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public UIElement? Content
    {
        get => (UIElement?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public event Action? BringToFrontRequested;
    public event Action? CloseRequested;

    private bool _isDragging;
    private Point _dragStart;
    private bool _isMaximized;
    private double _restoreLeft, _restoreTop, _restoreWidth, _restoreHeight;

    private Grid? _titleBar;
    private Button? _closeBtn;
    private Button? _maxBtn;
    private Button? _minBtn;
    private Thumb? _resizeThumb;
    private ContentPresenter? _contentPresenter;

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();

        _titleBar = GetTemplateChild("PART_TitleBar") as Grid;
        _closeBtn = GetTemplateChild("PART_Close") as Button;
        _maxBtn = GetTemplateChild("PART_Maximize") as Button;
        _minBtn = GetTemplateChild("PART_Minimize") as Button;
        _resizeThumb = GetTemplateChild("PART_ResizeThumb") as Thumb;
        _contentPresenter = GetTemplateChild("PART_Content") as ContentPresenter;

        if (_titleBar != null)
        {
            _titleBar.MouseLeftButtonDown += TitleBar_MouseLeftButtonDown;
            _titleBar.MouseMove += TitleBar_MouseMove;
            _titleBar.MouseLeftButtonUp += TitleBar_MouseLeftButtonUp;
        }

        if (_closeBtn != null)
            _closeBtn.Click += (_, _) => CloseRequested?.Invoke();

        if (_maxBtn != null)
            _maxBtn.Click += (_, _) => ToggleMaximize();

        if (_minBtn != null)
            _minBtn.Click += (_, _) => Visibility = Visibility.Collapsed;

        if (_resizeThumb != null)
        {
            _resizeThumb.DragDelta += ResizeThumb_DragDelta;
        }

        MouseLeftButtonDown += (_, _) => BringToFrontRequested?.Invoke();
    }

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 1 && !_isMaximized)
        {
            _isDragging = true;
            _dragStart = e.GetPosition(Parent as UIElement);
            _titleBar!.CaptureMouse();
        }
    }

    private void TitleBar_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging) return;
        var pos = e.GetPosition(Parent as UIElement);
        var delta = pos - _dragStart;
        _dragStart = pos;

        var left = Canvas.GetLeft(this) + delta.X;
        var top  = Canvas.GetTop(this) + delta.Y;
        Canvas.SetLeft(this, Math.Max(0, left));
        Canvas.SetTop(this, Math.Max(0, top));
    }

    private void TitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        _titleBar!.ReleaseMouseCapture();
    }


    private void ToggleMaximize()
    {
        if (Parent is not Canvas canvas) return;

        if (!_isMaximized)
        {
            _restoreLeft   = Canvas.GetLeft(this);
            _restoreTop    = Canvas.GetTop(this);
            _restoreWidth  = Width;
            _restoreHeight = Height;

            Canvas.SetLeft(this, 0);
            Canvas.SetTop(this, 0);
            Width  = canvas.ActualWidth;
            Height = canvas.ActualHeight;
            _isMaximized = true;
        }
        else
        {
            Canvas.SetLeft(this, _restoreLeft);
            Canvas.SetTop(this, _restoreTop);
            Width  = _restoreWidth;
            Height = _restoreHeight;
            _isMaximized = false;
        }
    }

    private void ResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        Width  = Math.Max(200, Width + e.HorizontalChange);
        Height = Math.Max(150, Height + e.VerticalChange);
    }
}
