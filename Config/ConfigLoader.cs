using Microsoft.Extensions.Configuration;
using ArchiveTransparency.Config;

namespace ArchiveTransparency.Config;

public static class ConfigLoader
{
    private static IConfigurationRoot? _configuration;

    public static IConfigurationRoot Configuration => _configuration ??= BuildConfiguration();

    public static Settings Settings => Configuration.Get<Settings>() ?? new Settings();

    private static IConfigurationRoot BuildConfiguration()
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        return builder.Build();
    }

    public static T GetSection<T>(string key) where T : class, new()
    {
        return Configuration.GetSection(key).Get<T>() ?? new T();
    }
}
