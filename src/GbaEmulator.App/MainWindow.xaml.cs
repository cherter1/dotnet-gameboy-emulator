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
    private readonly byte[] _pixelBytes;
    private readonly DispatcherTimer _timer;

    public MainWindow(EmulatorStartup startup)
    {
        InitializeComponent();

        _machine = startup.Machine;
        _pixelBytes = new byte[_machine.FrameBuffer.Width * _machine.FrameBuffer.Height * 4];

        _bitmap = new WriteableBitmap(
            _machine.FrameBuffer.Width,
            _machine.FrameBuffer.Height,
            96,
            96,
            System.Windows.Media.PixelFormats.Bgra32,
            null);

        FrameImage.Source = _bitmap;
        Title = startup.WindowTitle;
        StatusText.Text = startup.StatusMessage;

        _timer = new DispatcherTimer(DispatcherPriority.Render)
        {
            Interval = TimeSpan.FromMilliseconds(16.67)
        };
        _timer.Tick += OnFrameTick;
        _timer.Start();
        Closed += (_, _) => _timer.Stop();
    }

    private void OnFrameTick(object? sender, EventArgs e)
    {
        _machine.RunFrame();
        _machine.FrameBuffer.CopyToBgra32(_pixelBytes);
        _bitmap.WritePixels(
            new Int32Rect(0, 0, _machine.FrameBuffer.Width, _machine.FrameBuffer.Height),
            _pixelBytes,
            _machine.FrameBuffer.Width * 4,
            0);
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
