using System.IO;
using System.Windows;
using AgentRelay.Core;

namespace AgentRelay.Desktop;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        var app = new Application();
        try
        {
            if (args.Length != 0 && (args.Length != 2 || args[0] != "--home"))
                throw new RelayException("invalid_arguments", "用法：AgentRelay.Desktop.exe [--home <data directory>]");
            var home = args.Length == 2 ? args[1] : Environment.GetEnvironmentVariable("RELAY_HOME") ??
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "AgentRelay");
            var service = new RelayService(home);
            service.Initialize();
            return app.Run(new MainWindow(service));
        }
        catch (Exception error) when (error is RelayException or IOException or UnauthorizedAccessException or Microsoft.Data.Sqlite.SqliteException)
        {
            MessageBox.Show(error.Message, "Agent Relay — Startup Error", MessageBoxButton.OK, MessageBoxImage.Error);
            return 2;
        }
    }
}
