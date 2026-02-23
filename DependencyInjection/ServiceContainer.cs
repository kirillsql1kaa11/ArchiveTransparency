using Microsoft.Extensions.DependencyInjection;
using Serilog;
using ArchiveTransparency.Config;
using ArchiveTransparency.Services;
using ArchiveTransparency.Windows;

namespace ArchiveTransparency.DependencyInjection;

public static class ServiceContainer
{
    private static IServiceProvider? _serviceProvider;

    public static IServiceProvider Services => _serviceProvider ??= BuildServiceProvider();

    public static T GetRequiredService<T>() where T : notnull
        => Services.GetRequiredService<T>();

    public static T? GetService<T>()
        => Services.GetService<T>();

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();

        // Configuration
        services.AddSingleton(ConfigLoader.Configuration);
        services.AddSingleton(ConfigLoader.Settings);

        // Logging
        Log.Logger = new LoggerConfiguration()
            .ReadFrom.Configuration(ConfigLoader.Configuration)
            .WriteTo.File(
                ConfigLoader.Settings.LogFilePath,
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        services.AddSingleton(Log.Logger);
        services.AddLogging();

        // Services
        services.AddSingleton<ArchiveReader>();
        services.AddSingleton<StatisticsService>();
        services.AddSingleton<HotkeyService>();

        // Windows (created as singletons for the app lifetime)
        services.AddSingleton<TooltipWindow>();
        services.AddSingleton<ExplorerMonitor>();

        // App
        services.AddSingleton<App>();

        return services.BuildServiceProvider();
    }
}
