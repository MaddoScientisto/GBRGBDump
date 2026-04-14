using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Infrastructure;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.ViewModels;
using GBTools.ImageSharp.GameBoyCamera.Avalonia.Views;
using Microsoft.Extensions.DependencyInjection;

namespace GBTools.ImageSharp.GameBoyCamera.Avalonia;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            GlobalExceptionHandlers.RegisterUiThreadHandler();

            MainWindow mainWindow = AppBootstrapper.Services.GetRequiredService<MainWindow>();
            mainWindow.DataContext = AppBootstrapper.Services.GetRequiredService<MainWindowViewModel>();

            MainWindowProvider windowProvider = AppBootstrapper.Services.GetRequiredService<MainWindowProvider>();
            windowProvider.MainWindow = mainWindow;

            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}