using System.Windows;
using GbaEmulator.App.Hosting;

namespace GbaEmulator.App;

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
