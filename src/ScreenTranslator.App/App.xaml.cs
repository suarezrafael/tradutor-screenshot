using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ScreenTranslator.Application;
using ScreenTranslator.Application.Abstractions;
using ScreenTranslator.Desktop.Interop;
using ScreenTranslator.Desktop.Logging;
using ScreenTranslator.Infrastructure.LanguageDetection;
using ScreenTranslator.Infrastructure.Ocr;
using ScreenTranslator.Infrastructure.ScreenCapture;
using ScreenTranslator.Infrastructure.Settings;
using ScreenTranslator.Infrastructure.Translation;

namespace ScreenTranslator.Desktop;

public partial class App : System.Windows.Application
{
    private IHost? _host;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var builder = Host.CreateApplicationBuilder();
        ConfigureLogging(builder);
        ConfigureServices(builder.Services);

        _host = builder.Build();
        await _host.StartAsync();

        var toolbar = _host.Services.GetRequiredService<ToolbarWindow>();
        toolbar.Show();
    }

    private static void ConfigureLogging(HostApplicationBuilder builder)
    {
        var logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ScreenTranslator", "logs");
        Directory.CreateDirectory(logDirectory);
        var logFilePath = Path.Combine(logDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");

        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(new FileLoggerProvider(logFilePath));
        builder.Logging.AddDebug();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton<IScreenCaptureService, Win32ScreenCaptureService>();
        services.AddSingleton<IOcrService, TesseractOcrService>();
        services.AddSingleton<ILanguageDetectionService, HeuristicLanguageDetectionService>();
        services.AddSingleton<ITranslationService, GTranslateService>();
        services.AddSingleton<ITranslationCache, MemoryTranslationCache>();
        services.AddSingleton<ITranslationOverlayService, TranslationOverlayService>();
        services.AddSingleton<IAppSettingsStore, JsonAppSettingsStore>();
        services.AddSingleton<PhraseGroupingService>();
        services.AddSingleton<CaptureTranslationOrchestrator>();

        services.AddSingleton<GlobalHotkeyManager>();
        services.AddSingleton<TrayIconService>();
        services.AddSingleton<CaptureFlowController>();
        services.AddSingleton<ToolbarWindow>();
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            _host.Services.GetService<TrayIconService>()?.Dispose();
            _host.Services.GetService<GlobalHotkeyManager>()?.Dispose();
            await _host.StopAsync();
            _host.Dispose();
        }

        base.OnExit(e);
    }
}
