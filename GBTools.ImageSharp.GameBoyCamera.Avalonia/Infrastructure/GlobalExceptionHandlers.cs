using Avalonia.Threading;
using Microsoft.Extensions.Logging;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia.Infrastructure;

internal static class GlobalExceptionHandlers
{
    private static ILogger? _logger;
    private static bool _uiHandlerRegistered;

    public static void Register(ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger("GlobalExceptionHandler");

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
    }

    public static void RegisterUiThreadHandler()
    {
        if (_uiHandlerRegistered)
        {
            return;
        }

        Dispatcher.UIThread.UnhandledException += (_, args) =>
        {
            _logger?.LogError(args.Exception, "Unhandled UI thread exception.");
        };

        _uiHandlerRegistered = true;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
    {
        if (args.ExceptionObject is Exception exception)
        {
            _logger?.LogCritical(exception, "Unhandled AppDomain exception.");
        }
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs args)
    {
        _logger?.LogError(args.Exception, "Unobserved task exception.");
    }
}