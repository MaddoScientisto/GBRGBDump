using Avalonia;
using System;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia;

sealed class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        AppBootstrapper.Initialize();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            AppBootstrapper.Dispose();
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
