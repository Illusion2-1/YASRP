using Microsoft.Extensions.DependencyInjection;
using YASRP.Core.Abstractions;
using YASRP.Core.Configurations.Models;
using YASRP.Core.Configurations.Provider;
using YASRP.Diagnostics.Logging.Providers;
using YASRP.Network.Dns.Caching;

namespace YASRP;

internal class Program {
    private static async Task Main(string[] args) {
        LogConfigurator.Initialize();
        var logger = LogWrapperFactory.CreateLogger(nameof(Program));

        logger.Info("Started.");

        var services = new ServiceCollection();

        var (_, serviceProvider) = ConfigureServices(services);

        var certManager = serviceProvider.GetRequiredService<ICertManager>();
        await certManager.InitializeAsync("example.org");


        var resolver = serviceProvider.GetRequiredService<IDoHResolver>();
        var cacheService = serviceProvider.GetRequiredService<IDnsCacheService>();

        var dnsList1 = resolver.QueryIpAddress("huggingface.co").Result;
        foreach (var ip in dnsList1) logger.Debug(ip);

        var dnsList2 = resolver.QueryIpAddress("files.illusionrealm.com").Result;
        foreach (var ip in dnsList2) logger.Debug(ip);


        cacheService.Persist();
    }

    private static (AppConfiguration, ServiceProvider) ConfigureServices(IServiceCollection services) {
        services.AddConfiguration()
            .AddCertManager()
            .AddDoHResolver();

        var tempProvider = services.BuildServiceProvider();
        var configProvider = tempProvider.GetRequiredService<IConfigurationProvider>();
        var config = configProvider.LoadConfiguration();

        services.AddSingleton(config);

        return (config, services.BuildServiceProvider());
    }
}