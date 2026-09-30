using System.Windows;
using GbaEmulator.App.Wpf.Hosting;

namespace GbaEmulator.App.Wpf;

public partial class App
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var startup = EmulatorStartup.Create(e.Args, AppContext.BaseDirectory);
        var window = new MainWindow(startup);
        MainWindow = window;
        window.Show();
    }
}
