using GBTools.ImageSharp.GameBoyCamera.Avalonia.Infrastructure;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Services;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;
using NLog.Targets;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia;

internal static class AppBootstrapper
{
    private static ServiceProvider? _services;

    public static ServiceProvider Services => _services ?? throw new InvalidOperationException("Services have not been initialized.");

    public static void Initialize()
    {
        if (_services is not null)
        {
            return;
        }

        ConfigureLogging();

        ServiceCollection serviceCollection = new();
        serviceCollection.AddLogging(static builder =>
        {
            builder.ClearProviders();
            builder.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            builder.AddNLog();
        });

        serviceCollection.AddSingleton<MainWindowProvider>();
        serviceCollection.AddSingleton<IBitmapFactory, BitmapFactory>();
        serviceCollection.AddSingleton<IAlbumLoadService, AlbumLoadService>();
        serviceCollection.AddSingleton<IDialogService, DialogService>();
        serviceCollection.AddSingleton<IImageExportService, ImageExportService>();
        serviceCollection.AddSingleton<MainWindowViewModel>();
        serviceCollection.AddSingleton<MainWindow>();

        _services = serviceCollection.BuildServiceProvider();
        GlobalExceptionHandlers.Register(_services.GetRequiredService<ILoggerFactory>());
    }

    public static void Dispose()
    {
        if (_services is IAsyncDisposable asyncDisposable)
        {
            asyncDisposable.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
        else
        {
            _services?.Dispose();
        }

        _services = null;
        LogManager.Shutdown();
    }

    private static void ConfigureLogging()
    {
        LoggingConfiguration configuration = new();

        ConsoleTarget consoleTarget = new("console")
        {
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}",
        };

        DebuggerTarget debuggerTarget = new("debugger")
        {
            Layout = "${longdate}|${level:uppercase=true}|${logger}|${message} ${exception:format=tostring}",
        };

        configuration.AddTarget(consoleTarget);
        configuration.AddTarget(debuggerTarget);
        configuration.AddRule(NLog.LogLevel.Info, NLog.LogLevel.Fatal, consoleTarget);
        configuration.AddRule(NLog.LogLevel.Trace, NLog.LogLevel.Fatal, debuggerTarget);

        LogManager.Configuration = configuration;
    }
}