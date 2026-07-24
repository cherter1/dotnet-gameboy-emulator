using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using GbaEmulator.App.Hosting;
using GbaEmulator.Core;
using GbaEmulator.Core.Input;

namespace GbaEmulator.App;

public partial class MainWindow
{
    private readonly GbaMachine _machine;
    private readonly WriteableBitmap _bitmap;
    private readonly Int32Rect _frameRect;
    private readonly byte[] _pixelBytes;
    private readonly int _stride;
    private readonly DispatcherTimer _timer;

    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private long _nextFrameTicks;
    private const double FramesPerSecond = 59.727500569606d;
    private static readonly long TicksPerFrame = (long)(Stopwatch.Frequency / FramesPerSecond);

    public MainWindow(EmulatorStartup startup)
    {
        InitializeComponent();

        _machine = startup.Machine;

        var width = _machine.FrameBuffer.Width;
        var height = _machine.FrameBuffer.Height;

        _stride = width * 4;
        _frameRect = new Int32Rect(0, 0, width, height);
        _pixelBytes = new byte[width * height * 4];

        _bitmap = new WriteableBitmap(
            width,
            height,
            96,
            96,
            System.Windows.Media.PixelFormats.Bgra32,
            null);

        FrameImage.Source = _bitmap;
        Title = startup.WindowTitle;
        StatusText.Text = startup.StatusMessage;

        _nextFrameTicks = _clock.ElapsedTicks;
        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(1)
        };
        _timer.Tick += OnFrameTick;
        _timer.Start();
        Closed += (_, _) =>
        {
            _timer.Stop();
            _timer.Tick -= OnFrameTick;
        };
    }

    private void OnFrameTick(object? sender, EventArgs e)
    {
        long now = _clock.ElapsedTicks;
        if (now < _nextFrameTicks)
        {
            return;
        }

        _machine.RunFrame();
        _machine.FrameBuffer.CopyToBgra32(_pixelBytes);
        _bitmap.WritePixels(
            _frameRect,
            _pixelBytes,
            _stride,
            0);

        _nextFrameTicks += TicksPerFrame;

        if (now - _nextFrameTicks > TicksPerFrame * 5)
        {
            _nextFrameTicks = now + TicksPerFrame;
        }
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (!TryMapKey(e.Key, out var button)) return;

        _machine.Keypad.SetPressed(button, true);
        e.Handled = true;
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (!TryMapKey(e.Key, out var button)) return;

        _machine.Keypad.SetPressed(button, false);
        e.Handled = true;
    }

    private static bool TryMapKey(Key key, out GbaButton button)
    {
        switch (key)
        {
            case Key.X:
                button = GbaButton.A;
                return true;
            case Key.Z:
                button = GbaButton.B;
                return true;
            case Key.A:
                button = GbaButton.L;
                return true;
            case Key.S:
                button = GbaButton.R;
                return true;
            case Key.Enter:
                button = GbaButton.Start;
                return true;
            case Key.RightShift:
            case Key.LeftShift:
                button = GbaButton.Select;
                return true;
            case Key.Up:
                button = GbaButton.Up;
                return true;
            case Key.Down:
                button = GbaButton.Down;
                return true;
            case Key.Left:
                button = GbaButton.Left;
                return true;
            case Key.Right:
                button = GbaButton.Right;
                return true;
            default:
                button = default;
                return false;
        }
    }
}
