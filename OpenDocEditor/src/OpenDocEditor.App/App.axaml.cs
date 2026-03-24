using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenDocEditor.App.ViewModels;
using OpenDocEditor.App.Views;
using OpenDocEditor.Core.Plugins;
using OpenDocEditor.Core.Services.Documents;
using OpenDocEditor.Core.Services.EDM;
using OpenDocEditor.Core.Services.Signature;
using Serilog;

namespace OpenDocEditor.App;

public partial class App : Application
{
    private IServiceProvider? _services;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Настройка логирования
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "OpenDocEditor", "logs", "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 10)
            .CreateLogger();
    }

    public override void OnFrameworkInitializationCompleted()
    {
        _services = BuildServiceProvider();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainVm = _services.GetRequiredService<MainWindowViewModel>();
            var mainWindow = new MainWindow { DataContext = mainVm };
            desktop.MainWindow = mainWindow;

            // Открыть аргументы командной строки
            var args = desktop.Args ?? [];
            if (args.Length > 0 && File.Exists(args[0]))
                _ = mainVm.OpenDocumentFromPath(args[0]);
            else
                mainVm.NewDocumentCommand.Execute(null);

            desktop.Exit += (_, _) =>
            {
                _services.GetService<PluginManager>()?.ShutdownAll();
                Log.CloseAndFlush();
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(lb => lb.AddSerilog(dispose: true));

        // Document services
        services.AddSingleton<IDocxReaderService, DocxReaderService>();
        services.AddSingleton<IDocxWriterService, DocxWriterService>();
        services.AddSingleton<IPdfExportService, PdfExportService>();
        services.AddSingleton<IDocumentService, DocumentService>();

        // EDM / Signature (заглушки — заменяются реальными реализациями через DI)
        services.AddSingleton<IEdmService, StubEdmService>();
        services.AddSingleton<ISignatureService, StubSignatureService>();

        // Plugins
        services.AddSingleton<PluginManager>();

        // ViewModels
        services.AddTransient<MainWindowViewModel>();

        var provider = services.BuildServiceProvider();

        // Загрузка плагинов
        var pluginsDir = Path.Combine(AppContext.BaseDirectory, "plugins");
        var pluginMgr = provider.GetRequiredService<PluginManager>();
        pluginMgr.DiscoverPlugins(pluginsDir, services);

        return provider;
    }
}
